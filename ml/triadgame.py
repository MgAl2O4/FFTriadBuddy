from xml.dom import minidom
from enum import Enum
import numpy as np


###############################################################################
# Cards
#
triadCardDB = []
triadCardDB_Limited = []
triadCardDB_LimitedBest = []
triadCardDB_Common = []
triadCardDB_CommonBest = []

class TriadCard:
    def __init__(self, name, rarity, cardType, sideUp, sideLeft, sideDown, sideRight):
        self.name = name
        self.rarity = rarity
        self.cardType = cardType
        self.sides = [ sideUp, sideLeft, sideDown, sideRight ]
        self.valid = True

    def __str__(self):
        if self.valid == False:
            return "invalid"    
        return 'type:%s, sides:[T:%i, L:%i, D:%i, R:%i], name:%s %s' % (
            self.cardType,
            self.sides[0], self.sides[1], self.sides[2], self.sides[3],
            self.name, '*' * (self.rarity + 1))

    @staticmethod
    def loadDB():
        rarityList = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
        typeList = ['None', 'Primal', 'Scion', 'Beastman', 'Garlean']

        unknownCard = TriadCard('<Hidden>', 0, 0, 0, 0, 0, 0)
        unknownCard.valid = False
        triadCardDB.append(unknownCard)
        
        cardXml = minidom.parse('../assets/data/cards.xml')
        cardElems = cardXml.getElementsByTagName('card')
        for elem in cardElems:
            rarityStr = elem.attributes['rarity'].value
            cardTypeStr = elem.attributes['type'].value
            cardOb = TriadCard(
                elem.attributes['name'].value,
                rarityList.index(rarityStr),
                typeList.index(cardTypeStr),
                int(elem.attributes['up'].value),
                int(elem.attributes['lt'].value),
                int(elem.attributes['dn'].value),
                int(elem.attributes['rt'].value))

            triadCardDB.append(cardOb)
            if (cardOb.rarity == 2):
                triadCardDB_CommonBest.append(cardOb)
            if (cardOb.rarity == 4):
                triadCardDB_LimitedBest.append(cardOb)
                
            if (cardOb.rarity > 2):
                triadCardDB_Limited.append(cardOb)
            else:
                triadCardDB_Common.append(cardOb)
            

    @staticmethod
    def find(name):
        for card in triadCardDB:
            if (card.name == name):
                return card
        return triadCardDB[0]

# load cards on startup
TriadCard.loadDB()
print('Loaded cards database:',len(triadCardDB),'entries')

class TriadDeck():
    def __init__(self):
        self.cards = [ 0, 0, 0, 0, 0 ]
        self.visible = [ False, False, False, False, False ]
        self.numAvail = 0

    def hasCard(self, idx):
        return isinstance(self.cards[idx], TriadCard)

    def useCard(self, idx):
        card = self.cards[idx]
        self.cards[idx] = 0
        self.visible[idx] = True
        self.numAvail -= 1
        return card

    def makeAllVisible(self):
        self.visible = [ True, True, True, True, True ]

###############################################################################
# Game logic
#
class TriadGameState(Enum):
    Unknown = 0
    PlayerTurn = 1
    OpponentTurn = 2
    GameWin = 3
    GameDraw = 4
    GameLose = 5
    
class TriadGame:
    def __init__(self):
        self.board = [ 0, 0, 0, 0, 0, 0, 0, 0, 0 ]
        self.owner = [ -1, -1, -1, -1, -1, -1, -1, -1, -1 ]
        self.typeMod = [0, 0, 0, 0, 0]
        self.playerCards = TriadDeck()
        self.opponentCards = TriadDeck()
        self.mods = []
        self.numRestarts = 0
        self.numPlayerPlaced = 0
        self.state = TriadGameState.Unknown       

    def getNumByOwner(self, ownerId):
        sum = 0
        for item in self.owner:
            if (item == ownerId):
                sum += 1
        return sum

    def getNumCardsPlaced(self):
        return len(self.owner) - self.getNumByOwner(-1)

    def getCardSideValue(self, card, side):
        return max(0, min(10, card.sides[side] + self.typeMod[card.cardType]))        

    def getCardOppositeSideValue(self, card, side):
        return self.getCardSideValue(card, (side + 2) % 4)

    def restartGame(self):
        self.board = [ 0, 0, 0, 0, 0, 0, 0, 0, 0 ]
        self.owner = [ -1, -1, -1, -1, -1, -1, -1, -1, -1 ]
        self.typeMod = [0, 0, 0, 0, 0]
        self.numRestarts += 1
        self.numPlayerPlaced = 0

    def placePlayerCard(self, pos, idx):
        if (not self.playerCards.hasCard(idx) or self.owner[pos] >= 0):
            return False
        
        card = self.playerCards.useCard(idx)
        self.numPlayerPlaced += 1
        #print('DEBUG player place card:',pos,'at:',idx)
        self.placeCard(pos, card, 0)
        return True

    def placeOpponentCard(self, pos, idx):
        if (not self.opponentCards.hasCard(idx) or self.owner[pos] >= 0):
            return False

        card = self.opponentCards.useCard(idx)
        #print('DEBUG opponent place card:',pos,'at:',idx)
        self.placeCard(pos, card, 1)
        return True

    def placeCard(self, pos, card, owner):
        if ((self.owner[pos] < 0) and card.valid):
            self.setBoardRaw(pos, card, owner)

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

            for mod in self.mods:
                mod.onPostCaptures(self)

            numEmpty = self.getNumByOwner(-1)
            if (numEmpty == 0):
                self.onAllCardsPlaced()
                

    def setBoardRaw(self, pos, card, owner):
        self.board[pos] = card
        self.owner[pos] = owner
        self.state = TriadGameState.PlayerTurn if (owner == 1) else TriadGameState.OpponentTurn

    def onAllCardsPlaced(self):
        numPlayerCards = self.playerCards.numAvail + self.getNumByOwner(0)
        numPlayerOwnedToWin = 5
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

    def getNeis(self, pos):
        boardX = pos % 3
        boardY = int(pos / 3)
        # [up, left, down, right]
        return [
            -1 if (boardY <= 0) else TriadGame.getBoardPos(boardX, boardY - 1),
            -1 if (boardX >= 2) else TriadGame.getBoardPos(boardX + 1, boardY),
            -1 if (boardY >= 2) else TriadGame.getBoardPos(boardX, boardY + 1),
            -1 if (boardX <= 0) else TriadGame.getBoardPos(boardX - 1, boardY)
            ]

    def getCaptures(self, pos, comboCounter):
        neis = self.getNeis(pos)
        #print('DEBUG getCaptures(',pos,', combo:',comboCounter,'), neis:',neis)
        comboList = []
        allowBasicCaptureCombo = (comboCounter > 0)
        if (comboCounter == 0):
            for mod in self.mods:
                comboListPart = mod.getCaptureNeis(self, pos, neis)
                #print('>> capture(',mod.name,') = ',comboListPart)
                if (len(comboListPart) > 0):
                    comboList = comboList + comboListPart
                    allowBasicCaptureCombo = True

        for side in range(4):
            neiPos = neis[side]
            #print('>> side:%i, neiPos:%i' % (side, neiPos))
            if ((neiPos >= 0) and (self.owner[neiPos] >= 0) and (self.owner[neiPos] != self.owner[pos])):
                sideV = self.getCardSideValue(self.board[pos], side)
                neiV = self.getCardOppositeSideValue(self.board[neiPos], side)
                for mod in self.mods:
                    sideV,neiV = mod.getCaptureWeights(self, sideV, neiV)                
                #print('    sideV:%i neiV:%i' % (sideV, neiV))
                
                capturedNei = sideV > neiV
                for mod in self.mods:
                    overrideCapture,newCaptureNei = mod.getCaptureCondition(self, sideV, neiV)
                    if overrideCapture:
                        capturedNei = newCaptureNei

                if capturedNei:
                    #print('    captured!')
                    self.owner[neiPos] = self.owner[pos]
                    if allowBasicCaptureCombo:
                        comboList.append(neiPos)

        return comboList
    

    def showState(self, debugName):
        print('[%s] ==> State: %s' % (debugName, self.state.name))
        for i in range(len(self.owner)):
            if (self.owner[i] < 0):
                print('  [%i] empty' % i)
            else:
                print('  [%i] %s owner:%i' % (i, str(self.board[i]), self.owner[i]))

        print('Type mods:', '(empty)' if (sum(self.typeMod) == 0) else '')
        for i in range(len(self.typeMod)):
            if (self.typeMod[i] != 0):
                print('  [%i] %i' % (i,self.typeMod[i]))

        print('Player hand:', '(empty)' if (self.playerCards.numAvail == 0) else '')
        for i in range(5):
            print('  [%i]: %s' % (i, str(self.playerCards.cards[i])))

        print('Opponent hand:', '(empty)' if (self.opponentCards.numAvail == 0) else '')
        for i in range(5):
            print('  [%i]: %s, vis:%s' % (i, str(self.opponentCards.cards[i]), self.opponentCards.visible[i]))

        print('Modifiers:', '(empty)' if (len(self.mods) == 0) else '')
        for i in range(len(self.mods)):
            print('  [%i]: %s' % (i, self.mods[i].name))        


###############################################################################
# MODIFIERS
#
triadModDB = []

class TriadMod:
    allowCombo = False
    name = '??'
    blockedMods = []        

    def onMatchStart(self, game):
        pass
    def onCardPlaced(self, game, pos):
        pass
    def onPostCaptures(self, game):
        pass
    def onAllCardsPlaced(self, game):
        pass    
    def getCaptureNeis(self, game, pos, neis):
        return []
    def getCaptureWeights(self, game, cardNum, neiNum):
        return cardNum, neiNum
    def getCaptureCondition(self, game, cardNum, neiNum):
        return False, False
    def getFilteredCards(self, game):
        return False, []
    def getFilteredPlacement(self, game):
        return False, []

# reverse: capture when number of lower
class TriadModReverse(TriadMod):
    def __init__(self):
        self.name = 'Reverse'
        
    def getCaptureCondition(self, game, cardNum, neiNum):
        return True, cardNum < neiNum

triadModDB.append(TriadModReverse())

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

triadModDB.append(TriadModFallenAce())

# same: capture when 2+ sides have the same values
class TriadModSame(TriadMod):
    def __init__(self):
        self.name = 'Same'
        self.allowCombo = True

    def getCaptureNeis(self, game, pos, neis):
        numSame = 0
        for side in range(4):
            neiPos = neis[side]
            if ((neiPos >= 0) and (game.owner[neiPos] >= 0)):
                sideV = game.getCardSideValue(game.board[pos], side)
                neiV = game.getCardOppositeSideValue(game.board[neiPos], side)
                if (sideV == neiV):
                    numSame += 1

        captureList = []
        if (numSame >= 2):
            cardOwner = game.owner[pos]
            for side in range(4):
                neiPos = neis[side]
                if ((neiPos >= 0) and (game.owner[neiPos] >= 0)):
                    if (game.owner[neiPos] != cardOwner):
                        game.owner[neiPos] = cardOwner
                        captureList.append(neiPos)

        return captureList

triadModDB.append(TriadModSame())

# plus: capture when 2+ sides have the same sum of values
class TriadModPlus(TriadMod):
    def __init__(self):
        self.name = 'Plus'
        self.allowCombo = True

    def getCaptureNeis(self, game, pos, neis):
        cardOwner = game.owner[pos]
        captureList = []
        for side in range(4):
            neiPos = neis[side]
            if ((neiPos >= 0) and (game.owner[neiPos] >= 0)):
                if (game.owner[neiPos] != cardOwner):
                    sideV = game.getCardSideValue(game.board[pos], side)
                    neiV = game.getCardOppositeSideValue(game.board[neiPos], side)
                    totalV = sideV + neiV
                    captured = False

                    for vsSide in range(4):
                        vsNeiPos = neis[vsSide]
                        if ((vsNeiPos >= 0) and (game.owner[vsNeiPos] >= 0) and (vsSide != side)):
                            vsSideV = game.getCardSideValue(game.board[pos], vsSide)
                            vsSideNeiV = game.getCardOppositeSideValue(game.board[vsNeiPos], vsSide)
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

triadModDB.append(TriadModPlus())

# ascention: type mod goes up after each placement
class TriadModAscention(TriadMod):
    def __init__(self):
        self.name = 'Ascention'
        self.blockedMods = ['Descention']
        
    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] += 1

triadModDB.append(TriadModAscention())

# descention: type mod goes down after each placement
class TriadModDescention(TriadMod):
    def __init__(self):
        self.name = 'Descention'
        self.blockedMods = ['Ascention']
        
    def onCardPlaced(self, game, pos):
        cardType = game.board[pos].cardType
        if (cardType> 0):
            game.typeMod[cardType] -= 1

triadModDB.append(TriadModDescention())

# sudden death: restart game on draw, keeping card ownership, max 3 times
class TriadModSuddenDeath(TriadMod):
    def __init__(self):
        self.name = 'SuddenDeath'

    def onAllCardsPlaced(self, game):
        if ((game.state == TriadGameState.GameDraw) and (game.numRestarts < 3)):
            for i in range(len(game.owner)):
                if (game.owner[i] < 0):
                    continue
                deck = game.playerCards if (game.owner[i] == 0) else game.opponentCards
                for freeIdx in range(5):
                    if not deck.hasCard(freeIdx):
                        deck.cards[freeIdx] = game.board[i]
                        break

            nextTurn = TriadGameState.PlayerTurn if (game.numPlayerPlaced < 5) else TriadGameState.OpponentTurn
            game.restartGame()            
            game.state = nextTurn            
            game.playerCards.makeAllVisible()
            game.playerCards.numAvail = 5
            game.opponentCards.makeAllVisible()
            game.opponentCards.numAvail = 5

triadModDB.append(TriadModSuddenDeath())

# all open: makes all opponent cards on hand visible
class TriadModAllOpen(TriadMod):
    def __init__(self):
        self.name = 'AllOpen'
        self.blockedMods = ['ThreeOpen']

    def onMatchStart(self, game):
        game.opponentCards.makeAllVisible()

triadModDB.append(TriadModAllOpen())

# three open: makes random 3 opponent cards on hand visible
class TriadModThreeOpen(TriadMod):
    def __init__(self):
        self.name = 'ThreeOpen'
        self.blockedMods = ['AllOpen']

    def onMatchStart(self, game):
        random3 = np.random.choice(range(5), 3, False)
        for idx in random3:
            game.opponentCards.visible[idx] = True
        pass

triadModDB.append(TriadModThreeOpen())

# order: forces card selection order (sequential from hand)
class TriadModOrder(TriadMod):
    def __init__(self):
        self.name = 'Order'

    def getFilteredCards(self, game):
        deck = game.playerCards if (game.state == TriadGameState.PlayerTurn) else game.opponentCards
        for i in range(len(deck.cards)):
            if deck.hasCard(i):
                return True, [ i ]
        
        return False, [ 0 ]

triadModDB.append(TriadModOrder())
        
# chaos: forces board placement order (random)
class TriadModChaos(TriadMod):
    def __init__(self):
        self.name = 'Chaos'

    def getFilteredPlacement(self, game):
        availPos = []
        for i in range(len(game.owner)):
            if (game.owner[i] < 0):
                availPos.append(i)

        randIdx = np.random.randint(0, len(availPos))
        return True, [ availPos[randIdx] ]

triadModDB.append(TriadModChaos())

# swap: swaps a card between player and opponent (1 each) at match start
class TriadModSwap(TriadMod):
    def __init__(self):
        self.name = 'Swap'

    def onMatchStart(self, game):
        playerIdx = np.random.randint(0, 5)
        opponentIdx = np.random.randint(0, 5)

        swapCard = game.opponentCards.cards[opponentIdx]
        game.opponentCards.cards[opponentIdx] = game.playerCards.cards[playerIdx]
        game.opponentCards.visible[opponentIdx] = True
        game.playerCards.cards[playerIdx] = swapCard

triadModDB.append(TriadModSwap())

###############################################################################
# Game session for ML training
# - observation: getState()
# - take action: step()
#
class TriadGameSession():
    def __init__(self):
        self.initializeGame()
    
    def generateRandomDeck(self, numLimited = 1):
        deck = TriadDeck()
        deck.numAvail = 5
        deck.cards = []

        if (numLimited > 0):
            deck.cards += list(np.random.choice(triadCardDB_Limited, numLimited, False))

        numCommon = 5 - numLimited
        if (numCommon > 0):
            deck.cards += list(np.random.choice(triadCardDB_Common, numCommon, False))

        return deck


    def generateRandomDeck_Player(self):
        deck = TriadDeck()
        deck.numAvail = 5
        deck.cards = []
        deck.cards += list(np.random.choice(triadCardDB_LimitedBest, 1, False))
        deck.cards += list(np.random.choice(triadCardDB_CommonBest, 4, False))
        return deck

    def generateRandomDeck_StrongNPC(self):
        return self.generateRandomDeck(5)

    def generateMods(self, maxMods = 4):
        numMods = np.random.randint(0, maxMods + 1)
        mods = []
        blocked = []

        while len(mods) < numMods:
            idx = np.random.randint(0, len(triadModDB))
            testMod = triadModDB[idx]
            if any(testMod.name in s for s in blocked):
                continue

            mods.append(testMod)
            blocked += testMod.blockedMods
            blocked.append(testMod.name)

        return mods


    def initializeGame(self):
        self.game = TriadGame()
        self.game.mods = self.generateMods()
        self.game.opponentCards = self.generateRandomDeck_Player()
        self.game.playerCards = self.generateRandomDeck_Player()
        self.game.playerCards.makeAllVisible()

        for mod in self.game.mods:
            mod.onMatchStart(self.game)

        if (np.random.random() < 0.5):
            self.game.state = TriadGameState.OpponentTurn
            self.playRandomMove()
        else:        
            self.game.state = TriadGameState.PlayerTurn
            

    def getAvailPositions(self):
        for mod in self.game.mods:
            useOverride, forcedPos = mod.getFilteredPlacement(self.game)
            if useOverride:
                return forcedPos

        availPos = []
        for i in range(len(self.game.owner)):
            if (self.game.owner[i] < 0):
                availPos.append(i)

        return availPos

    def getAvailCards(self):
        for mod in self.game.mods:
            useOverride, forcedIndices = mod.getFilteredCards(self.game)
            if useOverride:
                return forcedIndices

        deck = self.game.playerCards if (self.game.state == TriadGameState.PlayerTurn) else self.game.opponentCards
        availCardIndices = []
        for i in range(5):
            if deck.hasCard(i):
                availCardIndices.append(i)

        return availCardIndices
    

    def getAvailActions(self):
        listCards = self.getAvailCards()
        listPos = self.getAvailPositions()
        #print('cards:',listCards,'pos:',listPos)
        
        if (len(listCards) > 0 and len(listPos) > 0):
            return [(itCard + (itPos * 5)) for itPos in listPos for itCard in listCards]
        return []

    def getMaxActions(self):
        return 5 * 9

    def getCardState(self, card):
        if isinstance(card, TriadCard):
            return card.sides
        return [0, 0, 0, 0]

    def getState(self):
        state = []

        # 0/1 for each game mod type
        allGameMods = [mod.name for mod in triadModDB]
        activeMods = [mod.name for mod in self.game.mods]
        state += [1 if (s in activeMods) else 0 for s in allGameMods]
        
        # value of type modes
        state += self.game.typeMod
        
        # owner,card data for each board cell
        for i in range(len(self.game.owner)):
            state += [ self.game.owner[i] ]
            state += self.getCardState(self.game.board[i])
        
        # card data for player
        state += [ self.game.playerCards.numAvail ]
        for i in range(5):
            state += self.getCardState(self.game.playerCards.cards[i])
        
        # card data for opponent
        state += [ self.game.opponentCards.numAvail ]
        for i in range(5):
            if self.game.opponentCards.visible[i]:
                state += self.getCardState(self.game.playerCards.cards[i])
            else:
                state += [ -1, -1, -1, -1 ]

        return state

    def playRandomMove(self):
        if (self.game.state == TriadGameState.OpponentTurn):
            actions = self.getAvailActions()
            action = np.random.choice(actions)
            
            cardIdx = action % 5
            boardPos = int(action / 5)
            self.game.placeOpponentCard(boardPos, cardIdx)
            

    def isFinished(self):
        return (self.game.state == TriadGameState.GameWin) or (self.game.state == TriadGameState.GameDraw) or (self.game.state == TriadGameState.GameLose)

    def step(self, action):
        cardIdx = action % 5
        boardPos = int(action / 5)
        
        placed = self.game.placePlayerCard(boardPos, cardIdx)
        reward = -100 if not placed else self.game.getNumByOwner(0)
        
        self.playRandomMove()
        state = self.getState()
        done = self.isFinished()

        return state, reward, done
