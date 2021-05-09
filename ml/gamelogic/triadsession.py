from triaddeck import TriadDeck
from triadgame import TriadGame, TriadGameState, TriadMod
from tqdm import tqdm
import random

###############################################################################
# Game session for ML training
# - observation: getState()
# - take action: step()
#

class TriadGameSession():
    def __init__(self):
        self.initializeGame()
        self.debug = False
        self.hasErrors = False

    def generateMods(self, maxMods = 4):
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

        return []#mods

    def initializeGame(self):
        self.game = TriadGame()
        self.game.mods = self.generateMods()
        self.game.opponentDeck = TriadDeck.generateDeckPlayer()
        self.game.playerDeck = TriadDeck.generateDeckPlayer()
        self.game.playerDeck.makeAllVisible()

        for mod in self.game.mods:
            mod.onMatchStart(self.game)

        self.game.cacheCaptureConditions()
        self.game.state = TriadGameState.PlayerTurn if (random.random() < 0.5) else TriadGameState.OpponentTurn
        self.game.onTurnStart()

    def getAvailPositions(self):
        if (self.game.forcedBoardIdx >= 0):
            return [ self.game.forcedBoardIdx ]

        availPos = []
        for i in range(len(self.game.owner)):
            if (self.game.owner[i] == 0):
                availPos.append(i)

        return availPos

    def getAvailCards(self):
        if (self.game.forcedCardIdx >= 0):
            return [ self.game.forcedCardIdx ]

        deck = self.game.playerDeck if (self.game.state == TriadGameState.PlayerTurn) else self.game.opponentDeck
        availCardIndices = []
        for i in range(5):
            if deck.hasCard(i):
                availCardIndices.append(i)

        return availCardIndices

    def getAvailActions(self):
        listCards = self.getAvailCards()
        listPos = self.getAvailPositions()

        if (len(listCards) > 0 and len(listPos) > 0):
            return [(itCard + (itPos * 5)) for itPos in listPos for itCard in listCards]
        return []

    def getMaxActions(self):
        return 5 * 9

    def getCardState(self, cardInfo):
        # [ available, known, type or 0, meta type, meta sides 0..3 (will be = to sides if known), avg power, meta rarirty ]
        # meta: avail, known, type or 0, avg power, meta rarity
        state = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
        metaState = [ 0, 0, 0, 0, 0 ]

        if cardInfo[0] == 1:
            cardMeta = cardInfo[3]
            state = [1, cardInfo[1], 0, cardMeta.typePct, cardMeta.sides[0], cardMeta.sides[1], cardMeta.sides[2], cardMeta.sides[3], cardMeta.avgPower, cardMeta.rarity]
            if cardInfo[2] != None:
                state[2] = cardInfo[2].cardType
            metaState = [1, cardInfo[1], state[2], cardMeta.avgPower, cardMeta.rarity]

        return state, metaState

    def getState(self):
        state = []
        useDefenceValues = True

        # precalc values
        moveOwnerId = TriadGame.ownerPlayer if self.game.state == TriadGameState.PlayerTurn else TriadGame.ownerOpponent
        oppOwnerId = -moveOwnerId
        deckMove = self.game.playerDeck if self.game.state == TriadGameState.PlayerTurn else self.game.opponentDeck
        deckOpp = self.game.opponentDeck if self.game.state == TriadGameState.PlayerTurn else self.game.playerDeck
        cardInfoMove = deckMove.getCardsInfo()
        cardInfoOpp = deckOpp.getCardsInfo()

        defenceMove = None
        defenceOpp = None
        if useDefenceValues:
            defenceMove, defenceOpp = self.evalDefences(moveOwnerId, oppOwnerId, cardInfoMove, cardInfoOpp)

        # one-hot: active modifiers
        allGameMods = [mod.name for mod in TriadMod.modDB]
        activeMods = [mod.name for mod in self.game.mods]
        state += [1 if (s in activeMods) else 0 for s in allGameMods]
        # value of type modes
        state += self.game.typeMod

        # one-hot: valid board placement
        if self.game.forcedBoardIdx >= 0:
            state += [1 if (pos == self.game.forcedBoardIdx) else 0 for pos in range(len(self.game.owner))]
        else:
            state += [1 if (ownerId == 0) else 0 for ownerId in self.game.owner]

        # one-hot: valid cards for move's owner
        if self.game.forcedCardIdx >= 0:
            state += [1 if (cardIdx == self.game.forcedCardIdx) else 0 for cardIdx in range(5)]
        else:
            state += [1 if (cardState != TriadDeck.cardNone) else 0 for cardState in deckMove.state]

        metaState = state.copy()

        # board cells: [ relative ownerId, type, sides 0..3, defence eval..]
        # meta cell: [ relative ownerId, type, avg sides, defence evals in 0.25 increments]
        for pos in range(len(self.game.owner)):
            cellInfo = [0, 0, 0, 0, 0, 0]
            metaInfo = [0, 0, 0]
            if self.game.owner[pos] != 0:
                cardOb = self.game.board[pos]
                cellInfo = [1 if self.game.owner[pos] == moveOwnerId else -1, cardOb.cardType, cardOb.sides[0], cardOb.sides[1], cardOb.sides[2], cardOb.sides[3]]
                metaInfo = [cellInfo[0], cellInfo[1], (cardOb.sides[0] + cardOb.sides[1] + cardOb.sides[2] + cardOb.sides[3]) / 4]

            state += cellInfo
            metaState += metaInfo
            if useDefenceValues:
                state += defenceMove[pos]
                state += defenceOpp[pos]
                for defIdx in range(len(defenceMove[pos])):
                    metaState.append(int(defenceMove[pos][defIdx] * 4) / 4)
                    metaState.append(int(defenceOpp[pos][defIdx] * 4) / 4)

        # card data for move owner & opponent
        for i in range(5):
            cardMS, cardMM = self.getCardState(cardInfoMove[i])
            cardOS, cardOM = self.getCardState(cardInfoOpp[i])
            state += cardMS
            state += cardOS
            metaState += cardMM
            metaState += cardOM

        return state, metaState

    def evalSlotDefenceForOwner(self, pos, ownerId, oppCardsInfo, numOppCards):
        numValues = 0
        capturingCards = [ False ] * len(oppCardsInfo)
        capturingMetas = [ False ] * len(oppCardsInfo)

        for side,neiPos in TriadGame.cachedNeis[pos]:
            if self.game.owner[neiPos] == 0:
                numValues += self.game.findCapturingWeakness(pos, side)
                for cardIdx in range(len(oppCardsInfo)):
                    if oppCardsInfo[cardIdx][0] == 1:
                        if not capturingCards[cardIdx] and (oppCardsInfo[cardIdx][1] == 1):
                            capturingCards[cardIdx] = self.game.canCaptureWithCard(pos, oppCardsInfo[cardIdx][2], side)
                        if not capturingMetas[cardIdx]:
                            capturingMetas[cardIdx] = self.game.canCaptureWithMeta(pos, oppCardsInfo[cardIdx][3], side)

        numOppCards = max(1, numOppCards)
        pctCardsKnown = sum(1 for i in capturingCards if i) / numOppCards
        pctCardsMeta = sum(1 for i in capturingMetas if i) / numOppCards
        return [1 - (numValues / 40), pctCardsKnown, pctCardsMeta]

    def evalDefences(self, moveOwnerId, oppOwnerId, moveCardsInfo, oppCardsInfo):
        # data for board cell:
        # - avg defence if owned or 0 (side defence: num values that can capture it directly)
        # - % of cards in opposing deck that can capture it directly
        # - as above, but using meta card values

        resultMove = [[0, 0, 0]] * len(self.game.board)
        resultOpp = [[0, 0, 0]] * len(self.game.board)

        totalPlaced = sum(self.game.mapPlaced)
        isFinished = totalPlaced >= len(self.game.board)
        if isFinished:
            return resultMove, resultOpp

        numMoveCards = sum(1 for info in moveCardsInfo if info[0] == 1)
        numOppCards = sum(1 for info in oppCardsInfo if info[0] == 1)

        for pos in range(len(self.game.board)):
            if self.game.owner[pos] == moveOwnerId:
                resultMove[pos] = self.evalSlotDefenceForOwner(pos, moveOwnerId, oppCardsInfo, numOppCards)
            elif self.game.owner[pos] == oppOwnerId:
                resultOpp[pos] = self.evalSlotDefenceForOwner(pos, oppOwnerId, moveCardsInfo, numMoveCards)

        return resultMove, resultOpp

    def playRandomMove(self):
        actions = self.getAvailActions()
        if len(actions) == 0:
            self.hasErrors = True
            if self.debug:
                self.game.showState('ERROR!')
                print('Avail cards:',self.getAvailCards())
                print('Avail board:',self.getAvailPositions())
            return

        action = random.choice(actions)

        cardIdx = action % 5
        boardPos = int(action / 5)
        placed = False
        if (self.game.state == TriadGameState.OpponentTurn):
            placed = self.game.placeCardFromDeck(boardPos, cardIdx, TriadGame.ownerOpponent)
            if not placed:
                self.hasErrors = True
                if self.debug:
                    self.game.showState('ERROR!')
                    print('Avail cards:',self.getAvailCards())
                    print('Avail board:',self.getAvailPositions())
                return
        else:
            placed = self.game.placeCardFromDeck(boardPos, cardIdx, TriadGame.ownerPlayer)

        if placed:
            self.getState()
            self.game.onTurnStart()

    def playRandomGame(self):
        while not self.isFinished() and not self.hasErrors:
            if self.debug:
                self.game.showState('step')
            self.playRandomMove()

        if not self.hasErrors and self.debug:
            self.game.showState('done')

    def isFinished(self):
        return (self.game.state == TriadGameState.GameWin) or (self.game.state == TriadGameState.GameDraw) or (self.game.state == TriadGameState.GameLose)

    def step(self, action):
        cardIdx = action % 5
        boardPos = int(action / 5)
        if self.debug:
            print('step: requesting card:%i at %i' % (cardIdx, boardPos))

        placed = self.game.placeCardFromDeck(boardPos, cardIdx, TriadGame.ownerPlayer)
        done = self.isFinished()

        if placed:
            reward = self.game.getNumByOwner(TriadGame.ownerPlayer)
            if not done:
                self.game.onTurnStart()
                if self.debug:
                    print('step: play random move, turn:',str(self.game.state))
                self.playRandomMove()
        else:
            reward = -10

        state = self.getState()
        return state, reward, done


##############################################################################
# test me

def runTestPlayRandomGames(numGames, reproSeed):
    if reproSeed < 0:
        print('Running',numGames,'random test games...')

    for i in tqdm(range(numGames)):
        seed = random.randrange(999999)
        random.seed(seed)
        session = TriadGameSession()
        session.seed = seed

        if reproSeed < 0:
            session.playRandomGame()
        else:
            seed = reproSeed
            session.hasErrors = True

        if session.hasErrors:
            random.seed(seed)
            session = TriadGameSession()
            session.debug = True
            session.game.debug = True
            session.playRandomGame()
            if session.hasErrors:
                print('Repro seed:',seed)
            else:
                print('FIXED')
            break

def runTestBoardState(reproSeed):
    seed = reproSeed
    if (reproSeed < 0):
        seed = random.randrange(999999)
    random.seed(seed)

    session = TriadGameSession()
    for i in range(3):
        session.playRandomMove()
    session.game.showState('3 moves, seed:%i' % (seed))

    defenceBlue, defenceRed = session.evalDefences(
        TriadGame.ownerPlayer, TriadGame.ownerOpponent,
        session.game.playerDeck.getCardsInfo(),
        session.game.opponentDeck.getCardsInfo())

    print('Defence blue:', defenceBlue)
    print('Defence red:', defenceRed)

    #print('State:',session.getState())

if __name__ == "__main__":
    runTestPlayRandomGames(100000, reproSeed=-1)
    #runTestBoardState(reproSeed=-1)
