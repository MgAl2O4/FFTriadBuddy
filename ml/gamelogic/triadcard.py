from xml.dom import minidom
from enum import Enum

class TriadCard:
    def __init__(self, name, rarity, cardType, sideUp, sideLeft, sideDown, sideRight):
        self.name = name
        self.rarity = rarity
        self.cardType = cardType
        self.sides = [ sideUp, sideLeft, sideDown, sideRight ]
        self.valid = True
        self.id = 0

    def __str__(self):
        if self.valid == False:
            return "invalid"
        return 'type:%s, sides:[T:%i, L:%i, D:%i, R:%i], name:%s %s' % (
            self.cardType,
            self.sides[0], self.sides[1], self.sides[2], self.sides[3],
            self.name, '*' * (self.rarity + 1))

class TriadCardMeta:
    def __init__(self, sides, typePct, rarity):
        self.sides = sides
        self.avgPower = (sides[0] + sides[1] + sides[2] + sides[3]) / 4
        self.typePct = typePct
        self.rarity = rarity

    def __str__(self):
        return '[T:%i, L:%i, D:%i, R:%i], avg:%i, type:%f, R:%i' % (self.sides[0], self.sides[1], self.sides[2], self.sides[3], self.avgPower, self.typePct, self.rarity)

class TriadCardDB:
    typeList = ['None', 'Primal', 'Scion', 'Beastman', 'Garlean']
    rarityList = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
    cards = []
    # [rarity] = list of cards
    mapRarity = []
    # [rarity] = TriadCardMeta
    mapRarityMeta = []

    @staticmethod
    def load():
        if (len(TriadCardDB.cards) > 0):
            return

        unknownCard = TriadCard('<Hidden>', 0, 0, 0, 0, 0, 0)
        unknownCard.valid = False
        TriadCardDB.cards = [ unknownCard ]

        TriadCardDB.mapRarity = []
        for i in range(len(TriadCardDB.rarityList)):
            TriadCardDB.mapRarity.append([])

        cardXml = minidom.parse('../../assets/data/cards.xml')
        cardElems = cardXml.getElementsByTagName('card')
        for elem in cardElems:
            rarityStr = elem.attributes['rarity'].value
            cardTypeStr = elem.attributes['type'].value
            cardOb = TriadCard(
                elem.attributes['name'].value,
                TriadCardDB.rarityList.index(rarityStr),
                TriadCardDB.typeList.index(cardTypeStr),
                int(elem.attributes['up'].value),
                int(elem.attributes['lt'].value),
                int(elem.attributes['dn'].value),
                int(elem.attributes['rt'].value))

            cardOb.id = len(TriadCardDB.cards)
            TriadCardDB.cards.append(cardOb)
            TriadCardDB.mapRarity[cardOb.rarity].append(cardOb)

        for i in range(len(TriadCardDB.rarityList)):
            numR = len(TriadCardDB.mapRarity[i])
            if numR > 0:
                accSides = [0, 0, 0, 0]
                accType = 0
                for card in TriadCardDB.mapRarity[i]:
                    accSides[0] += card.sides[0]
                    accSides[1] += card.sides[1]
                    accSides[2] += card.sides[2]
                    accSides[3] += card.sides[3]
                    accType += 1 if (card.cardType != 0) else 0
                accSides[0] /= numR
                accSides[1] /= numR
                accSides[2] /= numR
                accSides[3] /= numR
                accType /= numR
                TriadCardDB.mapRarityMeta.append(TriadCardMeta(accSides, accType, i))

        print('Loaded cards database:',len(TriadCardDB.cards),'entries')
        for i in range(len(TriadCardDB.rarityList)):
            print(' %s: %d' % (TriadCardDB.rarityList[i], len(TriadCardDB.mapRarity[i])))

    @staticmethod
    def find(name):
        for card in TriadCardDB.cards:
            if (card.name == name):
                return card
        return TriadCardDB.cards[0]


# load cards on startup
TriadCardDB.load()

##############################################################################
# test me
if __name__ == "__main__":
    print('\nCard rarity stats:')
    for i in range(5):
        cardMeta = TriadCardDB.mapRarityMeta[i]
        print('>> %i: %s' % (i, cardMeta))
