﻿using UnityEngine;
using System.Collections;
using System;
using Frictionless;
using System.Collections.Generic;

public class UnitManager : MonoBehaviour
{
	private Dictionary<int, MelodyUnit> SelectedUnit;
    public CMCellGrid GameBoard;

	private MessageRouter MessageRouter;

	void Awake() {
		SelectedUnit = new Dictionary<int, MelodyUnit> ();
	}
	
    void Start()
    {
		MessageRouter = ServiceFactory.Instance.Resolve<MessageRouter>();
		MessageRouter.AddHandler<UnitActionMessage>(OnUnitAction);
		MessageRouter.AddHandler<SwitchPlayerMessage> (OnSwitchPlayer);
    }

    public CMCellGrid getGrid() {
    	return GameBoard;
    }

	void OnUnitAction(UnitActionMessage m) {
		switch (m.ActionType) {
			case UnitActionMessageType.SELECT:
				SwitchSelection(m.Color, m.PlayerNumber);
				break;
			case UnitActionMessageType.MOVE:
				MoveUnit(m.Direction, m.PlayerNumber);
				break;
			case UnitActionMessageType.ATTACK:
				Attack(m.Color, m.PlayerNumber);
				break;
		}
	}

	void SwitchSelection(InputButton color, int playerNumber) {
		if (SelectedUnit.ContainsKey(playerNumber) && SelectedUnit[playerNumber]) {
			UncolorEnemies();
			UncolorDirections (SelectedUnit[playerNumber].Cell);
		}
		
		SelectedUnit[playerNumber] = (GameBoard.Units.Find(c => (c.PlayerNumber == playerNumber) && 
			((c as MelodyUnit).ColorButton == color)) as MelodyUnit);

		if (SelectedUnit.ContainsKey(playerNumber)) {
			ColorDirections (SelectedUnit[playerNumber].Cell);
			ColorEnemies (playerNumber);
		}
	}

	void MoveUnit(Vector2 direction, int playerNumber) {
		if (SelectedUnit.ContainsKey(playerNumber) && SelectedUnit[playerNumber]) {
			Cell destination = GameBoard.Cells.Find(c => c.OffsetCoord == 
				SelectedUnit[playerNumber].Cell.OffsetCoord + direction);
			if (destination && !destination.IsTaken) {
				UncolorEnemies();
				UncolorDirections (SelectedUnit[playerNumber].Cell);
				SelectedUnit[playerNumber].Move(destination, 
					SelectedUnit[playerNumber].FindPath(GameBoard.Cells, destination));
				ColorDirections (destination);
				ColorEnemies (playerNumber);
				SelectedUnit[playerNumber].MovementPoints = int.MaxValue; // TODO: Wow such hack
			} else {
				MessageRouter.RaiseMessage(new RejectActionMessage { PlayerNumber = GameBoard.CurrentPlayerNumber, 
					ActionType = UnitActionMessageType.MOVE });
			}
		}
	}

	public void UncolorDirections(Cell cell) {
		List<Cell> neighbors = cell.GetNeighbours (GameBoard.Cells);
		foreach (Cell neighbor in neighbors) {
			neighbor.UnMark ();
		}
	}

	public void ColorDirections(Cell cell) {
		List<Cell> neighbors = cell.GetNeighbours (GameBoard.Cells);
		foreach (Cell neighbor in neighbors) {
			if (neighbor.IsTaken)
				continue;
			Vector2 offset = neighbor.OffsetCoord - cell.OffsetCoord;
			if (offset.x < 0) {
				(neighbor as CMCell).SetColor (Color.green);
			} else if (offset.x > 0) {
				(neighbor as CMCell).SetColor (Color.blue);
			} else if (offset.y < 0) {
				(neighbor as CMCell).SetColor (Color.yellow);
			} else {
				(neighbor as CMCell).SetColor (Color.red);
			}
		}	
	}

	public void UncolorEnemies() {		
		foreach (MelodyUnit enemy in GameBoard.Units) {
			enemy.UnMark ();
		}
	}

	public void ColorEnemies(int playerNumber) {		
		//foreach (Cell neighbor in GameBoard.Cells) {
		List<MelodyUnit> enemies;
		foreach (Unit enemy in GameBoard.Units) {
			if (enemy.PlayerNumber != playerNumber) {
			//if(neighbor.IsTaken) {
				//(SelectedUnit[playerNumber].Cell as CMCell).SetColor(Color.green);
				float offset = Math.Abs(enemy.Cell.OffsetCoord.x - SelectedUnit[playerNumber].Cell.OffsetCoord.x) + Math.Abs(enemy.Cell.OffsetCoord.y - SelectedUnit[playerNumber].Cell.OffsetCoord.y);
				//float offset = neighbor.OffsetCoord[0] - SelectedUnit[playerNumber].Cell.OffsetCoord[0] + neighbor.OffsetCoord[1] - SelectedUnit[playerNumber].Cell.OffsetCoord[1];
				Debug.Log(Math.Abs(offset));
				if (offset != 0 && offset <= SelectedUnit[playerNumber].GetAttackRange()) {
					Debug.Log("here");
					//(neighbor as CMCell).SetColor(Color.black);
					(enemy.Cell as CMCell).SetColor(Color.black);
					(SelectedUnit[playerNumber].Cell as CMCell).SetColor(Color.black);
					enemy.MarkAsReachableEnemy();
				}
			}
		}	
	}

	void Attack(InputButton color, int playerNumber) {
//		SelectedUnit[playerNumber] = GameBoard.Units[0] as MelodyUnit;
		MelodyUnit recipient = GameBoard.Units.Find(c => 
			(c.PlayerNumber != playerNumber) && 
			((c as MelodyUnit).ColorButton == color) &&
			(Math.Abs(SelectedUnit[playerNumber].Cell.OffsetCoord[0] - c.Cell.OffsetCoord[0])) <= c.AttackRange && 
			(Math.Abs(SelectedUnit[playerNumber].Cell.OffsetCoord[1] - c.Cell.OffsetCoord[1])) <= c.AttackRange)
			as MelodyUnit;
		if (recipient && SelectedUnit[playerNumber]) {
			SelectedUnit[playerNumber].DealDamage(recipient);
			if(recipient.HitPoints <= 0) {

			}
		} else {
			MessageRouter.RaiseMessage(new RejectActionMessage { PlayerNumber = GameBoard.CurrentPlayerNumber, ActionType = UnitActionMessageType.ATTACK });
		}
	}

	void OnSwitchPlayer(SwitchPlayerMessage m) {
		foreach (MelodyUnit cur in SelectedUnit.Values) {
			UncolorDirections (cur.Cell);
		}
		if (SelectedUnit.ContainsKey (m.PlayerNumber)) {
			ColorDirections (SelectedUnit [m.PlayerNumber].Cell);
		}
	}
}
