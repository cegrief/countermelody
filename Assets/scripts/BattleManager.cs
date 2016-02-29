﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Frictionless;
using System.Linq;
using System;

/// <summary>
/// Sent when a player hits a note
/// </summary>
public class NoteHitMessage {
	public int PlayerNumber{ get; set; }
	public int InstrumentID{ get; set; }  // Used by Song.cs to play/mute player's instrument track
}

/// <summary>
/// Sent when a player misses a note
/// </summary>
public class NoteMissMessage {
	public int PlayerNumber{ get; set; }
	public int InstrumentID{ get; set; }  // Used by Song.cs to play/mute player's instrument track
}

/// <summary>
/// Sent when a battle begins
/// </summary>
public class EnterBattleMessage {
	/// Right now this is just used by Song.cs to cue unmuting instrument tracks.
	public MelodyUnit AttackingUnit {get; set;}
	public MelodyUnit DefendingUnit {get; set;}
}

/// <summary>
/// Sent when a battle ends
/// </summary>
public class ExitBattleMessage {
	/// Right now this is just used by Song.cs to cue muting instrument tracks.
	public MelodyUnit AttackingUnit {get; set;}
	public MelodyUnit DefendingUnit {get; set;}
	public float AttackerHitPercent{ get; set; }
	public float DefenderHitPercent{ get; set; }
}

/// <summary>
/// Sent when a player wishes to change their difficulty level (Guitar Hero - like)
/// </summary>
public class BattleDifficultyChangeMessage {
	public int PlayerNumber {get; set;}
	public int Difficulty {get; set;}
}

public class BattleManager : MonoBehaviour {

	private class PlayerBattleData {
		public int instrumentID { get; set; }
		public int difficulty { get; set; }
		public Note[] battleNotes { get; set; }
		public int[] battleNoteStates { get; set; }
		public MelodyUnit unit {get; set;}
		public int playerNumber { get; set; }
		public bool hitLastNote { get; set; }
		public GameObject battleSprite { get; set; }
	}

	private Dictionary<int, PlayerBattleData> players;
	private MessageRouter messageRouter;
	private bool isInBattle;
	private PlayerBattleData attacker;
	private PlayerBattleData defender;
	private int battleProgressInMeasures; // Number of measures into the battle
	private MeshRenderer targetLine; // Renderer for note targets. Currently a temporary black line.
	private GameObject divider; // Divider between player's notes
	public  GameObject targetPrefab; // Single target object
	private GameObject[] targets;

	private float UNIT_MARGIN;

	public int battleMeasures = 1;
	public Camera parentCam;
	public GameObject notePrefab;
	public Vector2 velocityRange = new Vector2 (0.12f, 0.2f);

	//Constants
	private const float SPAWN_DEPTH = 13f;

	void Awake() {
		players = new Dictionary<int, PlayerBattleData> ();
	}

	void Start() {
		ServiceFactory.Instance.RegisterSingleton<BattleManager> (this);
		messageRouter = ServiceFactory.Instance.Resolve<MessageRouter> ();
		messageRouter.AddHandler<EnterBattleMessage> (OnStartBattle);
		messageRouter.AddHandler<ButtonDownMessage> (OnButtonDown);
		messageRouter.AddHandler<ButtonUpMessage> (OnButtonUp);
		messageRouter.AddHandler<BeatCenterMessage> (OnBeatCenter);
		messageRouter.AddHandler<ExitBeatWindowMessage> (OnExitBeatWindow);
		messageRouter.AddHandler<BattleDifficultyChangeMessage> (OnBattleDifficultyChange);
		targetLine = GameObject.Find ("Temp Battle Target Line").GetComponent<MeshRenderer> ();
		targetLine.enabled = false;
		targetLine.transform.localPosition = new Vector3(0, -5, SPAWN_DEPTH);

		//Add the divider
		divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
		divider.layer = LayerMask.NameToLayer ("BattleLayer");
		divider.transform.localScale = new Vector3(0.1f, 100f, 1f);
		divider.transform.position = parentCam.ScreenToWorldPoint(new Vector3(Screen.width / 2, 0, SPAWN_DEPTH));
		divider.GetComponent<MeshRenderer>().enabled = false;

		// Add targets
		targets = new GameObject[10];
		for (int i = 0; i < 2; i++) {
			for (int j = 0; j < 5; j++) {
				GameObject t = GameObjectUtil.Instantiate (targetPrefab);
				t.transform.parent = targetLine.transform;
				Target tc = t.GetComponent<Target> ();
				tc.player = i;
				tc.color = (InputButton)j;
				targets [i * 5 + j] = t;
			}
		}
	}

	/// <summary>
	/// Register a new player or update a player's difficulty / instrument setting
	/// </summary>
	/// <param name="playerNumber">Player number.</param>
	/// <param name="instrumentID">Instrument ID.</param>
	/// <param name="difficulty">Difficulty.</param>
	public void RegisterPlayer(int playerNumber, int instrumentID, int difficulty) {
		if (players.ContainsKey(playerNumber)) {
			players [playerNumber].instrumentID = instrumentID;
			players [playerNumber].difficulty = difficulty;
		} else {
			players.Add (playerNumber, new PlayerBattleData (){ instrumentID = instrumentID, difficulty = difficulty });
		}
	}

	/// <summary>
	/// Sets the player instrument.
	/// </summary>
	/// <param name="playerNumber">Player number.</param>
	/// <param name="instrumentID">Instrument I.</param>
	public void SetPlayerInstrument(int playerNumber, int instrumentID) {
		Debug.Assert (players.ContainsKey (playerNumber));

	}


	void OnStartBattle(EnterBattleMessage m) {

		// Constants
		float CENTER_MARGIN = Screen.width * 2 / 27f;
		UNIT_MARGIN = Screen.width * 3.5f / 27f;
		float FRET_RANGE = Screen.width / 3f; //TODO: Change based on number of players
		float SPAWN_HEIGHT = Screen.height;

		Song song = ServiceFactory.Instance.Resolve<Song>();

		// Reposition targets
		float targetXPos = UNIT_MARGIN + FRET_RANGE/10;
		for (int i = 0; i < 2; i++, targetXPos += CENTER_MARGIN) {
			for (int j = 0; j < 5; j++, targetXPos += FRET_RANGE/5) {
				GameObject t = targets [i * 5 + j];
				Vector3 screenPosition = parentCam.ScreenToWorldPoint (new Vector3 (targetXPos, 0, SPAWN_DEPTH));
				t.transform.position = new Vector3 (screenPosition.x, targetLine.transform.position.y, screenPosition.z);
			}
		}

		targetLine.enabled = true;
		divider.GetComponent<MeshRenderer>().enabled = true;
		Debug.Assert (players.ContainsKey (m.AttackingUnit.PlayerNumber));
		Debug.Assert (players.ContainsKey (m.DefendingUnit.PlayerNumber));
		// Prepare battle data per player
		attacker = players [m.AttackingUnit.PlayerNumber];
		defender = players [m.DefendingUnit.PlayerNumber];

		attacker.playerNumber = m.AttackingUnit.PlayerNumber;
		defender.playerNumber = m.DefendingUnit.PlayerNumber;

		attacker.unit = m.AttackingUnit;
		defender.unit = m.DefendingUnit;

		attacker.instrumentID = 0; // Edgy synth
		defender.instrumentID = 1; // Smoother synth

		RenderBattleSprites(m.AttackingUnit, m.DefendingUnit);
		
		attacker.battleNotes = song.GetNextBattleNotes (battleMeasures, attacker.instrumentID, attacker.difficulty);
		defender.battleNotes = song.GetNextBattleNotes (battleMeasures, defender.instrumentID, defender.difficulty);

		attacker.battleNoteStates = new int[attacker.battleNotes.Length];
		defender.battleNoteStates = new int[defender.battleNotes.Length];

		List<int> OrderedPlayers = players.Keys.ToList();
		OrderedPlayers.Sort();

		float PlayerXPos = UNIT_MARGIN - FRET_RANGE/10;

		float currentMusicTime = song.playerPosition;
		// Spawn notes:
		foreach (int playerNumber in OrderedPlayers) {
			float velocity = Math.Abs (velocityRange.x + (velocityRange.y - velocityRange.x) * players [playerNumber].difficulty / 2);
			foreach (Note note in players[playerNumber].battleNotes) {
				GameObject spawnedNote = GameObjectUtil.Instantiate(notePrefab);
				spawnedNote.transform.parent = parentCam.transform;
				spawnedNote.transform.position = parentCam.ScreenToWorldPoint(
					new Vector3(PlayerXPos + ((note.fretNumber+1) * FRET_RANGE / 5), SPAWN_HEIGHT, SPAWN_DEPTH));


				NoteObject NoteObject = spawnedNote.GetComponent<NoteObject> ();
				NoteObject.SetNoteColor(note);

				NoteObject.velocity = new Vector3 (0, -velocity, 0);

				float heightOffset = (note.getPositionTime(song.bpm) - currentMusicTime);
				while (heightOffset < 0) {
					heightOffset += song.totalSeconds;
				}

				Vector3 DistanceToTarget = new Vector3(0f, spawnedNote.transform.position.y - targetLine.transform.position.y, 0f);
				Vector3 StartingOffset = ((1 / Time.fixedDeltaTime) * heightOffset * NoteObject.velocity * -1)
					- DistanceToTarget - NoteObject.centerOfObject; //MATH BITCHES
				spawnedNote.transform.position += StartingOffset;

			}
			PlayerXPos += FRET_RANGE + CENTER_MARGIN;
		}

		isInBattle = true;
		battleProgressInMeasures = 0;
	}

	void Update() {
		if (isInBattle) {
			MarkPassedNotes(attacker);
			MarkPassedNotes(defender);
		}
	}

	void OnExitBeatWindow(ExitBeatWindowMessage m) {
		//this is where we will make a sprite in the fighting scene dance
		//attacker_unit.GetComponentInChildren<Animator>().SetTrigger("beat");
		//defender_unit.GetComponentInChildren<Animator>().SetTrigger("beat");
		if (isInBattle && m.BeatNumber == m.BeatsPerMeasure - 1) {
			battleProgressInMeasures++;
		}
	}

	void OnBeatCenter(BeatCenterMessage m) {
		// After the final beat of a battle sequence is played, trigger EndBattle
		if (isInBattle && m.BeatNumber == m.BeatsPerMeasure - 1 && battleProgressInMeasures == battleMeasures) {
			// Delay by half beat to allow any final sixteenth notes to be played
			StartCoroutine(EndBattleCoroutine(60f / m.BeatsPerMinute));
		}
	}

	IEnumerator EndBattleCoroutine(float delay) {
		// Delay by half beat to allow any final eigth notes to be played
		// TODO: Figure out if we can penalize spamming strum to hit every note
		yield return new WaitForSeconds(delay);
		targetLine.enabled = false;
		divider.GetComponent<MeshRenderer>().enabled = false;
		GameObjectUtil.Destroy(attacker.battleSprite);
		GameObjectUtil.Destroy(defender.battleSprite);
		isInBattle = false;
		int attackerHitCount = 0;
		foreach (int i in attacker.battleNoteStates) {
			if (i == 1)
				attackerHitCount++;
		}
		int defenderHitCount = 0;
		foreach (int i in defender.battleNoteStates) {
			if (i == 1)
				defenderHitCount++;
		}
		messageRouter.RaiseMessage (new ExitBattleMessage () {
			AttackingUnit = attacker.unit,
			DefendingUnit = defender.unit,
			AttackerHitPercent = (float) attackerHitCount / attacker.battleNotes.Length,
			DefenderHitPercent = (float) defenderHitCount / defender.battleNotes.Length
		});
	}

	protected void OnButtonDown(ButtonDownMessage m) {
		// Check if when the player strums that they actually hit something
		if (!isInBattle)
			return;
		int maxFret = int.MinValue;
		switch (m.Button) {
		case InputButton.STRUM:
			tryHitNote (true, m.PlayerNumber);
			break;
		case InputButton.GREEN:
			maxFret = 0;
			break;
		case InputButton.RED:
			maxFret = 1;
			break;
		case InputButton.YELLOW:
			maxFret = 2;
			break;
		case InputButton.BLUE:
			maxFret = 3;
			break;
		case InputButton.ORANGE:
			maxFret = 4;
			break;
		default:
			break;
		}
		if (maxFret >= 0 && players [m.PlayerNumber].hitLastNote) {
			InputButton[] frets = Interpreter.HeldFrets.ContainsKey (m.PlayerNumber) ? 
				Interpreter.HeldFrets [m.PlayerNumber].ToArray () : new InputButton[]{ };
			foreach (InputButton b in frets) {
				if ((int)b > maxFret) {
					return;
				}
			}
			if (!tryHitNote (false, m.PlayerNumber, maxFret)) {
				players [m.PlayerNumber].hitLastNote = false;
			}
		}
	}

	void OnButtonUp(ButtonUpMessage m) {
		// Check if when the player strums that they actually hit something
		if (!isInBattle)
			return;
		if (!players [m.PlayerNumber].hitLastNote)
			return;
		switch (m.Button) {
		case InputButton.GREEN:
		case InputButton.RED:
		case InputButton.YELLOW:
		case InputButton.BLUE:
		case InputButton.ORANGE:
			int maxFret = int.MinValue;
			InputButton[] frets = Interpreter.HeldFrets.ContainsKey (m.PlayerNumber) ? 
				Interpreter.HeldFrets [m.PlayerNumber].ToArray () : new InputButton[]{ };
			foreach (InputButton b in frets) {
				if (b != m.Button && (int)b > maxFret) {
					maxFret = (int)b;
				}
			}
			if (maxFret >= 0) {
				tryHitNote (false, m.PlayerNumber, maxFret);
			}
			break;
		default:
			break;
		}
	}

	bool tryHitNote(bool isStrum, int playerNumber, int hopoValue = -1) {
		InputButton[] frets = !isStrum ? new InputButton[]{ (InputButton)hopoValue } : Interpreter.HeldFrets.ContainsKey (playerNumber) ? 
			Interpreter.HeldFrets [playerNumber].ToArray () : new InputButton[]{ };
		Note[] hitNotes = ServiceFactory.Instance.Resolve<Song>().GetHitNotes (players [playerNumber].instrumentID, players [playerNumber].difficulty, frets);
		bool noteWasHit = false;
		// Go through the possible notes that the player could have been trying to hit
		GameObject[] noteObjects = GameObject.FindGameObjectsWithTag("noteObject");
		foreach (Note n in hitNotes) { // Warning! O(n^2)
			for (int i = 0; i < players [playerNumber].battleNotes.Length; i++) {
				if (n.Equals (players [playerNumber].battleNotes [i])) {
					if (players [playerNumber].battleNoteStates [i] == 0) {
						if (!isStrum && !n.isHOPO)
							continue;
						players [playerNumber].battleNoteStates [i] = 1;
						noteWasHit = true;
						players [playerNumber].hitLastNote = true;
						messageRouter.RaiseMessage (new NoteHitMessage () {
							PlayerNumber = playerNumber,
							InstrumentID = players [playerNumber].instrumentID
						});
						break;
					}
				}
			}
			if (noteWasHit) {
				// TODO: Make this less computationally expensive. Bad search.
				foreach (GameObject no in noteObjects) {
					NoteObject noc = no.GetComponent<NoteObject> ();
					if (noc.NoteData.Equals(n)) {
						GameObjectUtil.Destroy (no);
					}
				}
				break;
			}
		}
		if (!noteWasHit && isStrum) {
			players [playerNumber].hitLastNote = false;
			messageRouter.RaiseMessage (new NoteMissMessage () {
				PlayerNumber = playerNumber,
				InstrumentID = players [playerNumber].instrumentID
			});
		}
		return noteWasHit;
	}

	void OnBattleDifficultyChange(BattleDifficultyChangeMessage m) {
		if (!players.ContainsKey (m.PlayerNumber)) {
			players.Add (m.PlayerNumber, new PlayerBattleData ());
		}
		players [m.PlayerNumber].difficulty = m.Difficulty;
	}

	void MarkPassedNotes(PlayerBattleData player) {
		Note[] passedNotes = ServiceFactory.Instance.Resolve<Song>().GetPassedNotes(player.instrumentID, player.difficulty, Time.deltaTime);

		foreach (Note n in passedNotes) {
			int i = Array.IndexOf(player.battleNotes, n);
			if (i >= 0 && player.battleNoteStates[i] != 1) {
				player.battleNoteStates[i] = -1;
				player.hitLastNote = false;
				messageRouter.RaiseMessage (new NoteMissMessage () {
					PlayerNumber = player.playerNumber,
					InstrumentID = player.instrumentID
				});
			}
		}
	}

	public int getPlayerDifficulty(int playerNumber) {
		return players [playerNumber].difficulty;
	}
	
	void RenderBattleSprites(MelodyUnit AttackingUnit, MelodyUnit DefendingUnit) {
		attacker.battleSprite = GameObjectUtil.Instantiate(AttackingUnit.transform.FindChild("RotatedVisual").gameObject);
		defender.battleSprite = GameObjectUtil.Instantiate(DefendingUnit.transform.FindChild("RotatedVisual").gameObject);

		attacker.battleSprite.layer = LayerMask.NameToLayer("BattleLayer");
		defender.battleSprite.layer = LayerMask.NameToLayer("BattleLayer");

		attacker.battleSprite.transform.parent = parentCam.transform;
		defender.battleSprite.transform.parent = parentCam.transform;

		if (AttackingUnit.PlayerNumber < DefendingUnit.PlayerNumber) {
			attacker.battleSprite.transform.position = parentCam.ScreenToWorldPoint(new Vector3(UNIT_MARGIN / 2, Screen.height / 2, SPAWN_DEPTH/2));
			defender.battleSprite.transform.position = parentCam.ScreenToWorldPoint(new Vector3(Screen.width - (UNIT_MARGIN / 2), Screen.height / 2, SPAWN_DEPTH/2));
		}
		else {
			defender.battleSprite.transform.position = parentCam.ScreenToWorldPoint(new Vector3(UNIT_MARGIN / 2, Screen.height / 2, SPAWN_DEPTH/2));
			attacker.battleSprite.transform.position = parentCam.ScreenToWorldPoint(new Vector3(Screen.width - (UNIT_MARGIN / 2), Screen.height / 2, SPAWN_DEPTH/2));
		}

		attacker.battleSprite.transform.eulerAngles = new Vector3(0, 180);
		defender.battleSprite.transform.eulerAngles = new Vector3(0, 180);

	}
}