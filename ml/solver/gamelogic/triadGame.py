from .triadCard import TriadCardDB
from enum import Enum

class TriadGameState(Enum):
    Unknown = 0
    BlueTurn = 1
    RedTurn = 2
    EndBlueWin = 3
    EndDraw = 4
    EndRedWin = 5

class TriadGame:
    OwnerIdBlue = 1
    OwnerIdRed = 2

    ModTurnStart = 0
    ModCardPlaced = 1
    ModAllPlaced = 2
    ModCaptureNei = 3
    ModCaptureWeight = 4
    ModCaptureCondition = 5
    ModOverrides = 6

    cachedNeis = []

    def __init__(self):
        self.board = [ None ] * 9
        self.owner = [ 0 ] * 9
        self.typeMod = [ 0 ] * len(TriadCardDB.typeList)
        self.blueDeck = None
        self.redDeck = None
        self.mods = []
        self.modOverrides = [ False ] * TriadGame.ModOverrides
        self.numRestarts = 0
        self.mapPlaced = [ 0, 0, 0 ]
        self.forcedCardIdx = -1
        self.forcedBoardIdx = -1
        self.state = TriadGameState.Unknown
        self.cachedSideValues = [[ 0, 0, 0, 0]] * 9
        self.debug = False

    def getNumCardsPlaced(self):
        return sum(self.mapPlaced)

    def getNumCardsByOwner(self, ownerId):
        return sum([1 if (testId == ownerId) else 0 for testId in self.owner])

    def getCardSideValue(self, card, side):
        return max(1, min(10, card.sides[side] + self.typeMod[card.cardType]))

    def getCardOppositeSideValue(self, card, side):
        return self.getCardSideValue(card, (side + 2) % 4)

    def initModifiers(self):
        self.modOverrides = [ False ] * TriadGame.ModOverrides
        for mod in self.mods:
            mod.onMatchStart(self)
            for idx in mod.overrides:
                self.modOverrides[idx] = True

    def restartGame(self):
        self.board = [ None ] * 9
        self.owner = [ 0 ] * 9
        self.typeMod = [ 0 ] * len(TriadCardDB.typeList)
        self.numRestarts += 1
        self.mapPlaced = [ 0, 0, 0 ]
        self.cachedSideValues = [[ 0, 0, 0, 0]] * 9
        if self.debug:
            print('restartGame',self.numRestarts)

    def placeCardFromDeck(self, pos, idx, ownerId):
        deck = self.blueDeck if ownerId == TriadGame.OwnerIdBlue else self.redDeck
        isValid = self.isMoveValid(deck, pos, idx)
        if isValid:
            card = deck.useCard(idx)
            self.mapPlaced[ownerId] += 1
            self.placeCard(pos, card, ownerId)
            return True

        return False

    def isMoveValid(self, deck, pos, idx):
        if (self.forcedBoardIdx >= 0 and self.forcedBoardIdx != pos):
            return False
        if (self.forcedCardIdx >= 0 and self.forcedCardIdx != idx):
            return False
        if (not deck.hasCard(idx) or self.owner[pos] != 0):
            return False
        return True

    def placeCard(self, pos, card, ownerId):
        if self.debug:
            print('Place card:%s, pos:%i, ownerId:%i' % (card, pos, ownerId))

        if ((self.owner[pos] == 0) and card != None and card.valid):
            self.setBoardRaw(pos, card, ownerId)

            allowCombo = False
            if self.modOverrides[TriadGame.ModCardPlaced]:
                for mod in self.mods:
                    mod.onCardPlaced(self, pos)
                    allowCombo = allowCombo or mod.allowCombo

            comboCounter = 0
            comboList = self.getCaptures(pos, comboCounter)
            while (allowCombo and len(comboList) > 0):
                comboCounter += 1
                nextCombo = []
                for pos in comboList:
                    nextComboPart = self.getCaptures(pos, comboCounter)
                    nextCombo = nextCombo + nextComboPart
                comboList = nextCombo

            totalPlaced = sum(self.mapPlaced)
            if (totalPlaced == len(self.board)):
                self.onAllCardsPlaced()

            # count again, all placed may trigger sudden death rule
            totalPlaced = sum(self.mapPlaced)
            if (totalPlaced < len(self.board)):
                self.onTurnStart()
        else:
            print('ERROR: failed to place card:%s, pos:%i, ownerId:%i' % (card, pos, ownerId))
            self.showState('DEBUG')

    def setBoardRaw(self, pos, card, ownerId):
        self.board[pos] = card
        self.owner[pos] = ownerId
        self.state = TriadGameState.BlueTurn if (ownerId == TriadGame.OwnerIdRed) else TriadGameState.RedTurn
        self.updateCachedValuesForCard(pos)

    def updateCachedValuesForCard(self, pos):
        card = self.board[pos]
        if card != None:
            self.cachedSideValues[pos] = [ self.getCardSideValue(card, side) for side in range(4) ]
        else:
            self.cachedSideValues[pos] = [0, 0, 0, 0]

    def onTurnStart(self):
        self.forcedBoardIdx = -1
        self.forcedCardIdx = -1
        if (self.state == TriadGameState.BlueTurn or self.state == TriadGameState.RedTurn) and self.modOverrides[TriadGame.ModTurnStart]:
            for mod in self.mods:
                mod.onTurnStart(self)

    def onAllCardsPlaced(self):
        numPlayerCards = self.blueDeck.numAvail + self.getNumCardsByOwner(TriadGame.OwnerIdBlue)
        numPlayerOwnedToWin = 5
        if self.debug:
            print('onAllCardsPlaced, player:%i+%i vs %i' % (self.blueDeck.numAvail, self.getNumCardsByOwner(TriadGame.OwnerIdBlue), numPlayerOwnedToWin))

        if (numPlayerCards > numPlayerOwnedToWin):
            self.state = TriadGameState.EndBlueWin
        elif (numPlayerCards == numPlayerOwnedToWin):
            self.state = TriadGameState.EndDraw
        else:
            self.state = TriadGameState.EndRedWin

        if self.modOverrides[TriadGame.ModAllPlaced]:
            for mod in self.mods:
                mod.onAllCardsPlaced(self)


    @staticmethod
    def getBoardPos(x, y):
        return x + (y * 3)

    def getCaptures(self, pos, comboCounter):
        neiInfo = TriadGame.cachedNeis[pos]
        if self.debug:
            print('getCaptures(',pos,', combo:',comboCounter,'), neis:',neiInfo)

        comboList = []
        allowMods = comboCounter == 0

        if allowMods and self.modOverrides[TriadGame.ModCaptureNei]:
            for mod in self.mods:
                comboListPart = mod.getCaptureNeis(self, pos, neiInfo)
                if self.debug:
                    print('>> capture(',mod.name,') = ',comboListPart)

                if (len(comboListPart) > 0):
                    comboList = comboList + comboListPart

        for side, neiPos in neiInfo:
            if self.debug:
                print('>> side:%i, neiPos:%i' % (side, neiPos))

            if ((self.owner[neiPos] != 0) and (self.owner[neiPos] != self.owner[pos])):
                sideV = self.cachedSideValues[pos][side]
                neiV = self.cachedSideValues[neiPos][(side + 2) % 4] # opposite side

                if allowMods and self.modOverrides[TriadGame.ModCaptureWeight]:
                    for mod in self.mods:
                        sideV, neiV = mod.getCaptureWeights(self, sideV, neiV)

                if self.debug:
                    print('    sideV:%i neiV:%i' % (sideV, neiV))

                canCaptureNei = sideV > neiV

                if allowMods and self.modOverrides[TriadGame.ModCaptureCondition]:
                    for mod in self.mods:
                        overrideCapture, newcanCaptureNei = mod.getCaptureCondition(self, neiV, sideV)
                        if overrideCapture:
                            canCaptureNei = newcanCaptureNei

                if canCaptureNei:
                    if self.debug:
                        print('    captured!')

                    self.owner[neiPos] = self.owner[pos]
                    if comboCounter > 0:
                        comboList.append(neiPos)

        return comboList


    def showState(self, debugName):
        print('[%s] ==> State: %s' % (debugName, self.state.name))
        for i in range(len(self.owner)):
            if (self.owner[i] == 0):
                print('  [%i] empty' % i)
            else:
                print('  [%i] %s owner:%i' % (i, str(self.board[i]), self.owner[i]))

        print('Type mods:', '(empty)' if (sum(self.typeMod) == 0) else '')
        for i in range(len(self.typeMod)):
            if (self.typeMod[i] != 0):
                print('  [%i] %i' % (i,self.typeMod[i]))

        print('Player hand:', '(empty)' if (self.blueDeck == None or self.blueDeck.numAvail == 0) else '')
        if self.blueDeck != None:
            for i in range(5):
                print('  [%i]: %s' % (i, str(self.blueDeck.cards[i])))

        print('Opponent hand:', '(empty)' if (self.redDeck == None or self.redDeck.numAvail == 0) else '')
        if self.redDeck != None:
            for i in range(5):
                print('  [%i]: %s, state:%s' % (i, str(self.redDeck.cards[i]), self.redDeck.state[i]))

        print('Modifiers:', '(empty)' if (len(self.mods) == 0) else '')
        for i in range(len(self.mods)):
            print('  [%i]: %s' % (i, self.mods[i].name))

        if (self.forcedCardIdx >= 0 or self.forcedBoardIdx >= 0):
            print('Forced card:%i, placement:%i' % (self.forcedCardIdx, self.forcedBoardIdx))

    @staticmethod
    def staticInit():
        for i in range(9):
            boardX = i % 3
            boardY = int(i / 3)
            info = []
            # [up, left, down, right]
            if boardY > 0:
                info.append([0, TriadGame.getBoardPos(boardX, boardY - 1)])
            if boardX < 2:
                info.append([1, TriadGame.getBoardPos(boardX + 1, boardY)])
            if boardY < 2:
                info.append([2, TriadGame.getBoardPos(boardX, boardY + 1)])
            if boardX > 0:
                info.append([3, TriadGame.getBoardPos(boardX - 1, boardY)])

            TriadGame.cachedNeis.append(info)

TriadGame.staticInit()
