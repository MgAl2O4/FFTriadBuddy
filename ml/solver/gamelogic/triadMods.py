import random
from .triadDeck import TriadDeck
from .triadGame import TriadGame, TriadGameState
import numpy as np

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

    @staticmethod
    def generateMods(maxMods = 4):
        numMods = random.randrange(0, maxMods + 1)
        mods = []
        blocked = []

        while len(mods) < numMods:
            idx = random.randrange(0, len(TriadMod.modDB))
            testMod = TriadMod.modDB[idx]
            if any(testMod.name in s for s in blocked):
                continue

            mods.append(testMod)
            blocked += testMod.blockedMods
            blocked.append(testMod.name)

        return mods


# reverse: capture when number of lower
class TriadModReverse(TriadMod):
    def __init__(self):
        self.name = 'Reverse'
        self.overrides = [TriadGame.ModCaptureCondition]

    def getCaptureCondition(self, game, cardNum, neiNum):
        return True, cardNum < neiNum

TriadMod.modDB.append(TriadModReverse())

# fallen ace: 1 captures 10/A
class TriadModFallenAce(TriadMod):
    def __init__(self):
        self.name = 'FallenAce'
        self.overrides = [TriadGame.ModCaptureWeight]

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
        self.overrides = [TriadGame.ModCaptureNei]

    def getCaptureNeis(self, game, pos, neis):
        captureNeis = []
        sameNeis = []
        for side,neiPos in neis:
            if (game.owner[neiPos] != 0):
                sideV = game.cachedSideValues[pos][side]
                neiV = game.cachedSideValues[neiPos][(side + 2) % 4] # opp side
                if (sideV == neiV):
                    sameNeis.append(neiPos)
                    if (game.owner[neiPos] != game.owner[pos]):
                        captureNeis.append(neiPos)

        captureList = []
        if (len(sameNeis) >= 2) and (len(captureNeis) > 0):
            captureList = captureNeis

        return captureList

TriadMod.modDB.append(TriadModSame())

# plus: capture when 2+ sides have the same sum of values
class TriadModPlus(TriadMod):
    def __init__(self):
        self.name = 'Plus'
        self.allowCombo = True
        self.overrides = [TriadGame.ModCaptureNei]

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
        self.overrides = [TriadGame.ModCardPlaced]

    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] += 1

TriadMod.modDB.append(TriadModAscention())

# descention: type mod goes down after each placement
class TriadModDescention(TriadMod):
    def __init__(self):
        self.name = 'Descention'
        self.blockedMods = ['Ascention']
        self.overrides = [TriadGame.ModCardPlaced]

    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] -= 1

TriadMod.modDB.append(TriadModDescention())

# sudden death: restart game on draw, keeping card ownership, max 3 times
class TriadModSuddenDeath(TriadMod):
    def __init__(self):
        self.name = 'SuddenDeath'
        self.overrides = [TriadGame.ModAllPlaced]

    def onAllCardsPlaced(self, game):
        if ((game.state == TriadGameState.EndDraw) and (game.numRestarts < 3)):
            for i in range(len(game.owner)):
                if (game.owner[i] == 0):
                    continue
                deck = game.blueDeck if (game.owner[i] == TriadGame.OwnerIdBlue) else game.redDeck
                for freeIdx in range(5):
                    if not deck.hasCard(freeIdx):
                        deck.cards[freeIdx] = game.board[i]
                        deck.state[freeIdx] = TriadDeck.cardVisible
                        break

            nextTurn = TriadGameState.BlueTurn if (game.mapPlaced[TriadGame.OwnerIdBlue] < 5) else TriadGameState.RedTurn
            game.restartGame()
            game.state = nextTurn
            game.blueDeck.onRestart()
            game.redDeck.onRestart()

TriadMod.modDB.append(TriadModSuddenDeath())

# all open: makes all opponent cards on hand visible
class TriadModAllOpen(TriadMod):
    def __init__(self):
        self.name = 'AllOpen'
        self.blockedMods = ['ThreeOpen']
        self.overrides = []

    def onMatchStart(self, game):
        game.redDeck.makeAllVisible()

TriadMod.modDB.append(TriadModAllOpen())

# three open: makes random 3 opponent cards on hand visible
class TriadModThreeOpen(TriadMod):
    def __init__(self):
        self.name = 'ThreeOpen'
        self.blockedMods = ['AllOpen']
        self.overrides = []

    def onMatchStart(self, game):
        random3 = np.random.choice(range(5), 3, False)
        for idx in random3:
            game.redDeck.state[idx] = TriadDeck.cardVisible
        pass

TriadMod.modDB.append(TriadModThreeOpen())

# order: forces card selection order (sequential from hand)
class TriadModOrder(TriadMod):
    def __init__(self):
        self.name = 'Order'
        self.blockedMods = ['Chaos']
        self.overrides = [TriadGame.ModTurnStart]

    def onTurnStart(self, game):
        deck = game.blueDeck if (game.state == TriadGameState.BlueTurn) else game.blueDeck
        for i in range(len(deck.cards)):
            if deck.hasCard(i):
                game.forcedCardIdx = i
                break

TriadMod.modDB.append(TriadModOrder())

# chaos: forces board placement order (random)
class TriadModChaos(TriadMod):
    def __init__(self):
        self.name = 'Chaos'
        self.blockedMods = ['Order']
        self.overrides = [TriadGame.ModTurnStart]

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
        self.overrides = []

    def onMatchStart(self, game):
        playerIdx = np.random.randint(0, 5)
        opponentIdx = np.random.randint(0, 5)

        swapCard = game.redDeck.cards[opponentIdx]
        game.redDeck.cards[opponentIdx] = game.blueDeck.cards[playerIdx]
        game.redDeck.state[opponentIdx] = TriadDeck.cardVisible
        game.blueDeck.cards[playerIdx] = swapCard
        if game.debug:
            print('SWAP player[%i] <> opp[%i]' % (playerIdx, opponentIdx))

TriadMod.modDB.append(TriadModSwap())
print('Loaded game modifiers:',len(TriadMod.modDB))
