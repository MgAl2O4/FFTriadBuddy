from triadcard import TriadCard, TriadCardDB
from triaddeck import TriadDeck
from enum import Enum
import numpy as np

class TriadGameState(Enum):
    Unknown = 0
    PlayerTurn = 1
    OpponentTurn = 2
    GameWin = 3
    GameDraw = 4
    GameLose = 5

class TriadGame:
    ownerPlayer = 1
    ownerOpponent = -1
    cachedNeis = []

    def __init__(self):
        self.board = [ None ] * 9
        self.owner = [ 0 ] * 9
        self.typeMod = [ 0 ] * len(TriadCardDB.typeList)
        self.playerDeck = None
        self.opponentDeck = None
        self.mods = []
        self.numRestarts = 0
        self.mapPlaced = [ 0, 0, 0 ]
        self.cachedSideValues = [[ 0, 0, 0, 0]] * 9
        self.cachedCaptureConditions = None
        self.cachedCaptureWeakness = None
        self.forcedCardIdx = -1
        self.forcedBoardIdx = -1
        self.state = TriadGameState.Unknown
        self.debug = False

    def getNumCardsPlaced(self):
        return sum(self.mapPlaced)

    def getNumCardsByOwner(self, ownerId):
        return sum([1 if (testId == ownerId) else 0 for testId in self.owner])

    def getCardSideValue(self, card, side):
        return max(1, min(10, card.sides[side] + self.typeMod[card.cardType]))

    def getCardOppositeSideValue(self, card, side):
        return self.getCardSideValue(card, (side + 2) % 4)

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
        deck = self.playerDeck if ownerId == TriadGame.ownerPlayer else self.opponentDeck
        isValid = self.isMoveValid(deck, pos, idx)
        if isValid:
            card = deck.useCard(idx)
            self.mapPlaced[ownerId + 1] += 1
            self.placeCard(pos, card, ownerId)
        return isValid

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
        else:
            print('ERROR: failed to place card:%s, pos:%i, ownerId:%i' % (card, pos, ownerId))
            self.showState('DEBUG')

    def setBoardRaw(self, pos, card, ownerId):
        self.board[pos] = card
        self.owner[pos] = ownerId
        self.state = TriadGameState.PlayerTurn if (ownerId == TriadGame.ownerOpponent) else TriadGameState.OpponentTurn
        self.updateCachedValuesForCard(pos)

    def updateCachedValuesForCard(self, pos):
        card = self.board[pos]
        if card != None:
            self.cachedSideValues[pos] = [ self.getCardSideValue(card, side) for side in range(4) ]
        else:
            self.cachedSideValues[pos] = [0, 0, 0, 0]

    def updateEffectiveValuesForBoard(self):
        for pos in range(len(self.board)):
            self.updateCachedValuesForCard(pos)

    def onTurnStart(self):
        self.forcedBoardIdx = -1
        self.forcedCardIdx = -1
        if (self.state == TriadGameState.PlayerTurn or self.state == TriadGameState.OpponentTurn):
            for mod in self.mods:
                mod.onTurnStart(self)

    def onAllCardsPlaced(self):
        numPlayerCards = self.playerDeck.numAvail + self.getNumCardsByOwner(TriadGame.ownerPlayer)
        numPlayerOwnedToWin = 5
        if self.debug:
            print('onAllCardsPlaced, player:%i+%i vs %i' % (self.playerDeck.numAvail, self.getNumCardsByOwner(TriadGame.ownerPlayer), numPlayerOwnedToWin))

        if (numPlayerCards > numPlayerOwnedToWin):
            self.state = TriadGameState.GameWin
        elif (numPlayerCards == numPlayerOwnedToWin):
            self.state = TriadGameState.GameDraw
        else:
            self.state = TriadGameState.GameLose

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
        allowBasicCaptureCombo = (comboCounter > 0)
        if (comboCounter == 0):
            for mod in self.mods:
                comboListPart = mod.getCaptureNeis(self, pos, neiInfo)
                if self.debug:
                    print('>> capture(',mod.name,') = ',comboListPart)

                if (len(comboListPart) > 0):
                    comboList = comboList + comboListPart
                    allowBasicCaptureCombo = True


        for side, neiPos in neiInfo:
            if self.debug:
                print('>> side:%i, neiPos:%i' % (side, neiPos))

            if ((self.owner[neiPos] != 0) and (self.owner[neiPos] != self.owner[pos])):
                sideV = self.cachedSideValues[pos][side]
                neiV = self.cachedSideValues[neiPos][(side + 2) % 4] # opposite side
                if self.debug:
                    print('    sideV:%i neiV:%i' % (sideV, neiV))

                if self.cachedCaptureConditions[sideV][neiV]:
                    if self.debug:
                        print('    captured!')

                    self.owner[neiPos] = self.owner[pos]
                    if allowBasicCaptureCombo:
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

        print('Player hand:', '(empty)' if (self.playerDeck == None or self.playerDeck.numAvail == 0) else '')
        if self.playerDeck != None:
            for i in range(5):
                print('  [%i]: %s' % (i, str(self.playerDeck.cards[i])))

        print('Opponent hand:', '(empty)' if (self.opponentDeck == None or self.opponentDeck.numAvail == 0) else '')
        if self.opponentDeck != None:
            for i in range(5):
                print('  [%i]: %s, state:%s' % (i, str(self.opponentDeck.cards[i]), self.opponentDeck.state[i]))

        print('Modifiers:', '(empty)' if (len(self.mods) == 0) else '')
        for i in range(len(self.mods)):
            print('  [%i]: %s' % (i, self.mods[i].name))

        if (self.forcedCardIdx >= 0 or self.forcedBoardIdx >= 0):
            print('Forced card:%i, placement:%i' % (self.forcedCardIdx, self.forcedBoardIdx))

    def canSideBeCaptured(self, sideV, neiV):
        for mod in self.mods:
            sideV,neiV = mod.getCaptureWeights(self, sideV, neiV)

        capturedByNei = neiV > sideV
        for mod in self.mods:
            overrideCapture,newCapturedByNei = mod.getCaptureCondition(self, neiV, sideV)
            if overrideCapture:
                capturedByNei = newCapturedByNei

        return capturedByNei

    def cacheCaptureConditions(self):
        # condition[sideV][neiV] => can sideV capture neiV ?
        self.cachedCaptureConditions = [ False ]
        for sideV in range(1,11):
            result = [ False ]  * 11
            for neiV in range(1,11):
                for mod in self.mods:
                    sideV,neiV = mod.getCaptureWeights(self, sideV, neiV)

                result[neiV] = sideV > neiV
                for mod in self.mods:
                    overrideCapture,newCapturedNei = mod.getCaptureCondition(self, sideV, neiV)
                    if overrideCapture:
                        result[neiV] = newCapturedNei

            self.cachedCaptureConditions.append(result)

        # weakness[sideV] => how many different neiV can capture it
        self.cachedCaptureWeakness = [ 0 ] * 11
        for sideV in range(1,11):
            for neiV in range(1,11):
                if self.cachedCaptureConditions[neiV][sideV]:
                    self.cachedCaptureWeakness[sideV] += 1

    def findCapturingWeakness(self, pos, side):
        sideV = self.cachedSideValues[pos][side]
        return self.cachedCaptureWeakness[sideV]

    def canCaptureWithCard(self, pos, neiCard, side):
        sideV = self.cachedSideValues[pos][side]
        neiV = self.getCardOppositeSideValue(neiCard, side)
        return self.cachedCaptureConditions[neiV][sideV]

    def canCaptureWithMeta(self, pos, neiMeta, side):
        sideV = self.cachedSideValues[pos][side]
        # todo: include type?
        neiV = round(neiMeta.sides[(side + 2) % 4])
        return self.cachedCaptureConditions[neiV][sideV]

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

###############################################################################
# MODIFIERS
#

class TriadMod:
    allowCombo = False
    name = '??'
    blockedMods = []
    modDB = []

    def onMatchStart(self, game):
        pass
    def onTurnStart(self, game):
        pass
    def onCardPlaced(self, game, pos):
        pass
    def onAllCardsPlaced(self, game):
        pass
    def getCaptureNeis(self, game, pos, neis):
        return []
    def getCaptureWeights(self, game, cardNum, neiNum):
        return cardNum, neiNum
    def getCaptureCondition(self, game, cardNum, neiNum):
        return False, False


# reverse: capture when number of lower
class TriadModReverse(TriadMod):
    def __init__(self):
        self.name = 'Reverse'

    def getCaptureCondition(self, game, cardNum, neiNum):
        return True, cardNum < neiNum

TriadMod.modDB.append(TriadModReverse())

# fallen ace: 1 captures 10/A
class TriadModFallenAce(TriadMod):
    def __init__(self):
        self.name = 'FallenAce'

    def getCaptureWeights(self, game, cardNum, neiNum):
        if (cardNum == 10) and (neiNum == 1):
            return 0, 1
        elif (cardNum == 1) and (neiNum == 10):
            return 1, 0
        else:
            return cardNum, neiNum

TriadMod.modDB.append(TriadModFallenAce())

# same: capture when 2+ sides have the same values
class TriadModSame(TriadMod):
    def __init__(self):
        self.name = 'Same'
        self.allowCombo = True

    def getCaptureNeis(self, game, pos, neis):
        numSame = 0
        for side,neiPos in neis:
            if (game.owner[neiPos] != 0):
                sideV = game.cachedSideValues[pos][side]
                neiV = game.cachedSideValues[neiPos][(side + 2) % 4] # opp side
                if (sideV == neiV):
                    numSame += 1

        captureList = []
        if (numSame >= 2):
            cardOwner = game.owner[pos]
            for side,neiPos in neis:
                if (game.owner[neiPos] != 0):
                    if (game.owner[neiPos] != cardOwner):
                        game.owner[neiPos] = cardOwner
                        captureList.append(neiPos)

        return captureList

TriadMod.modDB.append(TriadModSame())

# plus: capture when 2+ sides have the same sum of values
class TriadModPlus(TriadMod):
    def __init__(self):
        self.name = 'Plus'
        self.allowCombo = True

    def getCaptureNeis(self, game, pos, neis):
        cardOwner = game.owner[pos]
        captureList = []
        for side,neiPos in neis:
            if game.owner[neiPos] != 0:
                if (game.owner[neiPos] != cardOwner):
                    sideV = game.cachedSideValues[pos][side]
                    neiV = game.cachedSideValues[neiPos][(side + 2) % 4] # opp side
                    totalV = sideV + neiV
                    captured = False

                    for vsSide,vsNeiPos in neis:
                        if ((game.owner[vsNeiPos] != 0) and (vsSide != side)):
                            vsSideV = game.cachedSideValues[pos][vsSide]
                            vsSideNeiV = game.cachedSideValues[vsNeiPos][(vsSide + 2) % 4] # opp side
                            vsSideTotalV = vsSideV + vsSideNeiV

                            if (vsSideTotalV == totalV):
                                captured = True
                                if (game.owner[vsNeiPos] != cardOwner):
                                    game.owner[vsNeiPos] = cardOwner
                                    captureList.append(vsNeiPos)

                    if captured:
                        game.owner[neiPos] = cardOwner
                        captureList.append(neiPos)

        return captureList

TriadMod.modDB.append(TriadModPlus())

# ascention: type mod goes up after each placement
class TriadModAscention(TriadMod):
    def __init__(self):
        self.name = 'Ascention'
        self.blockedMods = ['Descention']

    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] += 1
            game.updateEffectiveValuesForBoard()

TriadMod.modDB.append(TriadModAscention())

# descention: type mod goes down after each placement
class TriadModDescention(TriadMod):
    def __init__(self):
        self.name = 'Descention'
        self.blockedMods = ['Ascention']

    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] -= 1
            game.updateEffectiveValuesForBoard()

TriadMod.modDB.append(TriadModDescention())

# sudden death: restart game on draw, keeping card ownership, max 3 times
class TriadModSuddenDeath(TriadMod):
    def __init__(self):
        self.name = 'SuddenDeath'

    def onAllCardsPlaced(self, game):
        if ((game.state == TriadGameState.GameDraw) and (game.numRestarts < 3)):
            for i in range(len(game.owner)):
                if (game.owner[i] == 0):
                    continue
                deck = game.playerDeck if (game.owner[i] == TriadGame.ownerPlayer) else game.opponentDeck
                for freeIdx in range(5):
                    if not deck.hasCard(freeIdx):
                        deck.cards[freeIdx] = game.board[i]
                        deck.state[freeIdx] = TriadDeck.cardVisible
                        break

            nextTurn = TriadGameState.PlayerTurn if (game.mapPlaced[TriadGame.ownerPlayer + 1] < 5) else TriadGameState.OpponentTurn
            game.restartGame()
            game.state = nextTurn
            game.playerDeck.onRestart()
            game.opponentDeck.onRestart()

TriadMod.modDB.append(TriadModSuddenDeath())

# all open: makes all opponent cards on hand visible
class TriadModAllOpen(TriadMod):
    def __init__(self):
        self.name = 'AllOpen'
        self.blockedMods = ['ThreeOpen']

    def onMatchStart(self, game):
        game.opponentDeck.makeAllVisible()

TriadMod.modDB.append(TriadModAllOpen())

# three open: makes random 3 opponent cards on hand visible
class TriadModThreeOpen(TriadMod):
    def __init__(self):
        self.name = 'ThreeOpen'
        self.blockedMods = ['AllOpen']

    def onMatchStart(self, game):
        random3 = np.random.choice(range(5), 3, False)
        for idx in random3:
            game.opponentDeck.state[idx] = TriadDeck.cardVisible
        pass

TriadMod.modDB.append(TriadModThreeOpen())

# order: forces card selection order (sequential from hand)
class TriadModOrder(TriadMod):
    def __init__(self):
        self.name = 'Order'

    def onTurnStart(self, game):
        deck = game.playerDeck if (game.state == TriadGameState.PlayerTurn) else game.playerDeck
        for i in range(len(deck.cards)):
            if deck.hasCard(i):
                game.forcedCardIdx = i
                break

TriadMod.modDB.append(TriadModOrder())

# chaos: forces board placement order (random)
class TriadModChaos(TriadMod):
    def __init__(self):
        self.name = 'Chaos'

    def onTurnStart(self, game):
        availPos = []
        for i in range(len(game.owner)):
            if (game.owner[i] == 0):
                availPos.append(i)

        if (len(availPos) > 0):
            game.forcedBoardIdx = np.random.choice(availPos)


TriadMod.modDB.append(TriadModChaos())

# swap: swaps a card between player and opponent (1 each) at match start
class TriadModSwap(TriadMod):
    def __init__(self):
        self.name = 'Swap'

    def onMatchStart(self, game):
        playerIdx = np.random.randint(0, 5)
        opponentIdx = np.random.randint(0, 5)

        swapCard = game.opponentDeck.cards[opponentIdx]
        game.opponentDeck.cards[opponentIdx] = game.playerDeck.cards[playerIdx]
        game.opponentDeck.state[opponentIdx] = TriadDeck.cardVisible
        game.playerDeck.cards[playerIdx] = swapCard
        if game.debug:
            print('SWAP player[%i] <> opp[%i]' % (playerIdx, opponentIdx))

TriadMod.modDB.append(TriadModSwap())
print('Loaded game modifiers:',len(TriadMod.modDB))
