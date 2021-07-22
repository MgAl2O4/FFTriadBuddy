from gamelogic.triadCard import TriadCardDB
from gamelogic.triadDeck import TriadDeck
from gamelogic.triadGame import TriadGame, TriadGameState
from gamelogic.triadMods import TriadMod
import random

class GameSession():
    def __init__(self, useModifiers = False):
        self.randomSeed = 0
        self.useModifiers = useModifiers
        self.init()

    def init(self):
        self.game = TriadGame()
        if self.useModifiers:
            self.game.mods = TriadMod.generateMods()

        self.game.redDeck = TriadDeck.generateDeckPlayer()
        self.game.blueDeck = TriadDeck.generateDeckPlayer()
        self.game.blueDeck.makeAllVisible()

        self.game.state = TriadGameState.BlueTurn if (random.random() < 0.5) else TriadGameState.RedTurn
        self.game.initModifiers()
        self.game.onTurnStart()


    def step(self, action):
        cardIdx = int(action / 9)
        boardIdx = action % 9

        ownerId = TriadGame.OwnerIdBlue if (self.game.state == TriadGameState.BlueTurn) else TriadGame.OwnerIdRed
        result = self.game.placeCardFromDeck(boardIdx, cardIdx, ownerId)
        if not result:
            raise RuntimeError('Step failed, random seed:', self.randomSeed)

    def isFinished(self):
        return (self.game.state != TriadGameState.BlueTurn) and (self.game.state != TriadGameState.RedTurn)

    def getCurrentPlayer(self):
        return 0 if self.game.state == TriadGameState.BlueTurn else 1

    @staticmethod
    def getMaxActions():
        return 9 * 5

    def getAllowedActions(self, state):
        actions = []
        for idxB in range(9):
            # is board pos available? (avail flag = 1)
            if state[idxB] == 1:
                for idxC in range(5):
                    # is card available? (avail flag = 1)
                    if state[idxC + 9] == 1:
                        actions.append((idxC * 9) + idxB)
        return actions

    def getReward(self, playerId):
        if self.game.state == TriadGameState.EndBlueWin:
            return 1 if playerId == 0 else -1
        if self.game.state == TriadGameState.EndRedWin:
            return -1 if playerId == 0 else 1
        if self.game.state == TriadGameState.EndDraw:
            return 0.1
        return 0

    def getState(self, playerId):
        state = []

        # precalc values
        ownerPlayer = TriadGame.OwnerIdBlue if playerId == 0 else TriadGame.OwnerIdRed
        deckPlayer = self.game.blueDeck if playerId == 0 else self.game.redDeck
        deckOpp = self.game.redDeck if playerId == 1 else self.game.blueDeck
        cardInfoPlayer = deckPlayer.getCardsInfo(True)
        cardInfoOpp = deckOpp.getCardsInfo()

        # one-hot: valid board placement
        if self.game.forcedBoardIdx >= 0:
            state += [1 if (pos == self.game.forcedBoardIdx) else 0 for pos in range(len(self.game.owner))]
        else:
            state += [1 if (ownerId == 0) else 0 for ownerId in self.game.owner]

        # one-hot: valid cards for move's owner
        if self.game.forcedCardIdx >= 0:
            state += [1 if (cardIdx == self.game.forcedCardIdx) else 0 for cardIdx in range(5)]
        else:
            state += [1 if (cardState != TriadDeck.cardNone) else 0 for cardState in deckPlayer.state]

        # one-hot: active modifiers
        allGameMods = [mod.name for mod in TriadMod.modDB]
        activeMods = [mod.name for mod in self.game.mods]
        state += [1 if (s in activeMods) else 0 for s in allGameMods]
        # value of type modes
        state += self.game.typeMod

        # board cells: [ relative ownerId, type, sides 0..3]
        for pos in range(len(self.game.owner)):
            cellInfo = [0, 0, 0, 0, 0, 0]
            if self.game.owner[pos] != 0:
                cardOb = self.game.board[pos]
                cellInfo = [1 if self.game.owner[pos] == ownerPlayer else -1, cardOb.cardType, cardOb.sides[0], cardOb.sides[1], cardOb.sides[2], cardOb.sides[3]]

            state += cellInfo

        # card data for move owner & opponent
        for i in range(5):
            if cardInfoPlayer[i][0] != 0:
                sides = cardInfoPlayer[i][2].sides
                state += [ cardInfoPlayer[i][0], sides[0], sides[1], sides[2], sides[3], cardInfoPlayer[i][3] ]
            else:
                state += [ 0, 0, 0, 0, 0, 0 ]

            if cardInfoOpp[i][0] != 0:
                sides = cardInfoOpp[i][2].sides if (cardInfoOpp[i][2] != None) else TriadCardDB.mapAvgSides[cardInfoOpp[i][3]]
                state += [ cardInfoOpp[i][0], cardInfoOpp[i][1], sides[0], sides[1], sides[2], sides[3], cardInfoOpp[i][3] ]
            else:
                state += [ 0, 0, 0, 0, 0, 0, 0 ]

        return state
