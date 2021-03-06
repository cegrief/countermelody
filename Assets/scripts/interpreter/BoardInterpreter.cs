﻿using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Frictionless;

public class PauseGameMessage {
	public int playerNumber;
}

public class ResumeGameMessage {
	public int playerNumber;
}

public class BoardInterpreter : Interpreter {

	private bool IsAcceptingActions;
	private const bool SWITCH_UNIT_ON_END_BEAT = true;
	public static Dictionary<int, List<InputButton>> HeldThisBeat;
	private bool ignore;

	protected virtual void Awake() {
		HeldThisBeat = new Dictionary<int, List<InputButton>> ();
	}

	protected override void Start() {
		ignore = false;
		base.Start ();
		MessageRouter.AddHandler<UnitActionMessage>(OnUnitAction);
		MessageRouter.AddHandler<EnterBeatWindowMessage>(OnEnterBeatWindow);
		MessageRouter.AddHandler<ExitBeatWindowMessage>(OnExitBeatWindow);
		MessageRouter.AddHandler<SceneChangeMessage> (OnSceneChange);
	}

	protected override void OnSceneChange(SceneChangeMessage m) {
		base.OnSceneChange (m);
		ignore = true;
		StartCoroutine(RemoveHandlers());
	}

	IEnumerator RemoveHandlers() {
		yield return new WaitForEndOfFrame();
		MessageRouter.RemoveHandler<UnitActionMessage>(OnUnitAction);
		MessageRouter.RemoveHandler<EnterBeatWindowMessage>(OnEnterBeatWindow);
		MessageRouter.RemoveHandler<ExitBeatWindowMessage>(OnExitBeatWindow);
		MessageRouter.RemoveHandler<SceneChangeMessage> (OnSceneChange);
	}

	protected override void OnButtonDown(ButtonDownMessage m) {
		if (ignore) {
			return;
		}
		base.OnButtonDown (m);
		if (!enabled)
			return;
		switch (m.Button) {
			case InputButton.STRUM:
				if (m.PlayerNumber == CurrentPlayer && IsAcceptingActions) {
					if (HeldFrets.ContainsKey(m.PlayerNumber) && HeldFrets [CurrentPlayer].Count > 0) {
						MessageRouter.RaiseMessage (new UnitActionMessage () { 
							ActionType = UnitActionMessageType.ATTACK, 
							PlayerNumber = CurrentPlayer,
							Color = HeldFrets [CurrentPlayer] [HeldFrets[CurrentPlayer].Count - 1]
						});
					} else {
						MessageRouter.RaiseMessage (new UnitActionMessage () { 
							ActionType = UnitActionMessageType.ATTACK, 
							PlayerNumber = CurrentPlayer,
							Color = InputButton.NONE
						});
					}
				} else {
					MessageRouter.RaiseMessage(new RejectActionMessage() {
						ActionType = UnitActionMessageType.ATTACK,
						PlayerNumber = CurrentPlayer
					});
				}
				break;
			case InputButton.UP:
			case InputButton.DOWN:
			case InputButton.LEFT:
			case InputButton.RIGHT:
				if (m.PlayerNumber == CurrentPlayer && IsAcceptingActions) {
					MessageRouter.RaiseMessage(new UnitActionMessage() {
						ActionType = UnitActionMessageType.MOVE,
						PlayerNumber = CurrentPlayer,
						Direction = DirectionToVector(m.Button)
					});
				} else {
					MessageRouter.RaiseMessage(new RejectActionMessage() {
						ActionType = UnitActionMessageType.MOVE,
						PlayerNumber = CurrentPlayer
					});
				}
				break;
			case InputButton.TILT:
				if (m.PlayerNumber == CurrentPlayer) {
					MessageRouter.RaiseMessage(new UnitActionMessage() {
						ActionType = UnitActionMessageType.SPECIAL,
						PlayerNumber = CurrentPlayer
					});
				} // No reject sent since tilting guitar can be unintentional
				break;
		case InputButton.GREEN:
		case InputButton.RED:
		case InputButton.YELLOW:
		case InputButton.BLUE:
		case InputButton.ORANGE:
			// Add color fret to frets held during this beat:
			if (IsAcceptingActions) {
				if (!HeldThisBeat.ContainsKey (m.PlayerNumber)) {
					HeldThisBeat.Add (m.PlayerNumber, new List<InputButton> ());
				}
				HeldThisBeat [m.PlayerNumber].Add (m.Button);
			}
			break;
		case InputButton.PLUS:
			MessageRouter.RaiseMessage (new PauseGameMessage () { playerNumber = CurrentPlayer });
			break;
			default:
				break;
		}
	}

	protected override void OnButtonUp(ButtonUpMessage m) {
		base.OnButtonUp (m);
		if (!enabled)
			return;
		switch (m.Button) {
			case InputButton.GREEN:
			case InputButton.RED:
			case InputButton.YELLOW:
			case InputButton.BLUE:
			case InputButton.ORANGE:
				if (m.PlayerNumber == CurrentPlayer && IsAcceptingActions) {
					MessageRouter.RaiseMessage(new UnitActionMessage() {
						ActionType = UnitActionMessageType.SELECT,
						PlayerNumber = CurrentPlayer,
						Color = m.Button
					});
				}
				break;
			default:
				break;
		}
	}

	private void OnEnterBeatWindow(EnterBeatWindowMessage m) {
		if (!enabled)
			return;
		IsAcceptingActions = true;
	}

	private void OnExitBeatWindow(ExitBeatWindowMessage m) {
		if (!enabled)
			return;
		if (HeldThisBeat.ContainsKey(CurrentPlayer) && HeldThisBeat[CurrentPlayer].Count > 0 && IsAcceptingActions 
			&& SWITCH_UNIT_ON_END_BEAT) {
			MessageRouter.RaiseMessage(new UnitActionMessage() {
				ActionType = UnitActionMessageType.SELECT,
				PlayerNumber = CurrentPlayer,
				Color = HeldThisBeat[CurrentPlayer][HeldThisBeat[CurrentPlayer].Count - 1]
			});
			HeldThisBeat.Clear ();
		}

		IsAcceptingActions = false;
	}

	private void OnUnitAction(UnitActionMessage m) {
		if (!enabled)
			return;
		IsAcceptingActions = false;
	}

	public static Vector2 DirectionToVector(InputButton b) {
		switch (b) {
			case InputButton.LEFT:
				return new Vector2 (-1, 0); // Left
			case InputButton.UP:
				return new Vector2 (0, 1); // Up
			case InputButton.DOWN:
				return new Vector2 (0, -1); // Down
			case InputButton.RIGHT:
				return new Vector2 (1, 0); // Right
			default:
				return new Vector2 ();
		}
	}
}
