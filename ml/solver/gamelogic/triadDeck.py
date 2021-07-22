from .triadCard import *
import random

class TriadDeck():
    cardNone = 0
    cardHidden = 1
    cardVisible = 2

    def __init__(self):
        self.cards = [ None, None, None, None, None ]
        self.state = [ TriadDeck.cardNone, TriadDeck.cardNone, TriadDeck.cardNone, TriadDeck.cardNone, TriadDeck.cardNone ]
        self.usedRarityCount = [ 0 ] * len(TriadCardDB.rarityList)
        self.expectedRarityCount = [0, 0, 3, 1, 1]
        self.numAvail = 0

    def initialize(self, cards, state):
        self.cards = cards
        self.numAvail = len(cards)
        for i in range(len(self.state)):
            self.state[i] = state

    def hasCard(self, idx):
        return self.state[idx] != TriadDeck.cardNone

    def useCard(self, idx):
        card = self.cards[idx]
        self.cards[idx] = None
        self.state[idx] = TriadDeck.cardNone
        self.numAvail -= 1
        self.usedRarityCount[card.rarity] += 1
        return card

    def makeAllVisible(self):
        for i in range(len(self.state)):
            self.state[i] = TriadDeck.cardVisible

    def onRestart(self):
        self.numAvail = len(self.cards)
        self.usedRarityCount = [ 0 ] * len(TriadCardDB.rarityList)

    def getCardsInfo(self, forceVisible = False):
        result = []
        remainingRarity = self.expectedRarityCount.copy()
        hiddenList = []
        for i in range(len(self.cards)):
            if (self.state[i] == TriadDeck.cardNone):
                result.append([0, 0, None, 0])
            elif (self.state[i] == TriadDeck.cardVisible) or forceVisible:
                cardOb = self.cards[i]
                remainingRarity[cardOb.rarity] -= 1
                result.append([1, 1, cardOb, cardOb.rarity])
            else:
                result.append([])
                hiddenList.append(i)

        if len(hiddenList) > 0:
            for i in range(len(remainingRarity)):
                remainingRarity[i] -= self.usedRarityCount[i]

                if remainingRarity[i] < 0:
                    if i < (len(remainingRarity) - 1):
                        remainingRarity[i + 1] += remainingRarity[i]
                    remainingRarity[i] = 0
                else:
                    remainingRarity[i] = min(remainingRarity[i], len(hiddenList))
                    if remainingRarity[i] > 0:
                        for iR in range(remainingRarity[i]):
                            cardIdx = hiddenList.pop()
                            result[cardIdx] = [1, 0, None, i]

        return result

    def __str__(self):
        stateDesc = ['none','hidden','visible']
        desc = []
        for i in range(len(self.state)):
            desc += [ '[%i]:%s (%i*%s)' % (
                i,
                self.cards[i].name if isinstance(self.cards[i], TriadCard) else "--",
                (self.cards[i].rarity + 1) if isinstance(self.cards[i], TriadCard) else 0,
                "" if self.state[i] == TriadDeck.cardVisible else ", " + stateDesc[self.state[i]]
            )]

        return ', '.join(desc)

    @staticmethod
    def generateDeckForRarityRange(state, minRarity, maxRarity):
        cards = []
        while len(cards) < 5:
            rarity = random.randrange(minRarity, maxRarity + 1)
            cardIdx = random.randrange(len(TriadCardDB.mapRarity[rarity]))
            testCard = TriadCardDB.mapRarity[rarity][cardIdx]
            if (testCard in cards) == False:
                cards.append(testCard)

        deck = TriadDeck()
        deck.initialize(cards, state)
        return deck

    @staticmethod
    def generateDeckRandom(state):
        return TriadDeck.generateDeckForRarityRange(state, 0, 4)

    @staticmethod
    def generateDeckNpc(minRarity, maxRarity):
        return TriadDeck.generateDeckForRarityRange(TriadDeck.cardHidden, minRarity, maxRarity)

    @staticmethod
    def generateDeckPlayer():
        randR4 = random.randrange(len(TriadCardDB.mapRarity[4]))
        randR3 = random.randrange(len(TriadCardDB.mapRarity[3]))
        cards = [ TriadCardDB.mapRarity[4][randR4], TriadCardDB.mapRarity[3][randR3] ]

        while len(cards) < 5:
            rarity = random.randrange(3)
            cardIdx = random.randrange(len(TriadCardDB.mapRarity[rarity]))
            testCard = TriadCardDB.mapRarity[rarity][cardIdx]
            if (testCard in cards) == False:
                cards.append(testCard)

        deck = TriadDeck()
        deck.initialize(cards, TriadDeck.cardHidden)
        return deck
