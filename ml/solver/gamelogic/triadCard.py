from xml.dom import minidom

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

class TriadCardDB:
    typeList = ['None', 'Primal', 'Scion', 'Beastman', 'Garlean']
    rarityList = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
    cards = []
    # [rarity] = list of cards
    mapRarity = []
    mapAvgSides = []

    @staticmethod
    def load():
        assetFolder = '../../assets/data/'
        if (len(TriadCardDB.cards) > 0):
            return

        unknownCard = TriadCard('<Hidden>', 0, 0, 0, 0, 0, 0)
        unknownCard.valid = False
        TriadCardDB.cards = [ unknownCard ]

        TriadCardDB.mapRarity = []
        TriadCardDB.mapAvgSides = []
        for i in range(len(TriadCardDB.rarityList)):
            TriadCardDB.mapRarity.append([])
            TriadCardDB.mapAvgSides.append([1,1,1,1])

        cardNames = {}
        locXml = minidom.parse(assetFolder + 'loc.xml')
        locElems = locXml.getElementsByTagName('loc')
        for elem in locElems:
            locType = int(elem.attributes['type'].value)
            if locType == 3:
                cardNames[elem.attributes['id'].value] = elem.attributes['en'].value

        cardXml = minidom.parse(assetFolder + 'cards.xml')
        cardElems = cardXml.getElementsByTagName('card')
        for elem in cardElems:
            cardOb = TriadCard(
                cardNames[elem.attributes['id'].value],
                int(elem.attributes['rarity'].value),
                int(elem.attributes['type'].value),
                int(elem.attributes['up'].value),
                int(elem.attributes['lt'].value),
                int(elem.attributes['dn'].value),
                int(elem.attributes['rt'].value))

            cardOb.id = len(TriadCardDB.cards)
            TriadCardDB.cards.append(cardOb)
            TriadCardDB.mapRarity[cardOb.rarity].append(cardOb)

        for i in range(len(TriadCardDB.rarityList)):
            avgSidesAcc = [0, 0, 0, 0]
            for card in TriadCardDB.mapRarity[i]:
                avgSidesAcc[0] += card.sides[0]
                avgSidesAcc[1] += card.sides[1]
                avgSidesAcc[2] += card.sides[2]
                avgSidesAcc[3] += card.sides[3]

            for side in range(4):
                TriadCardDB.mapAvgSides[i][side] = avgSidesAcc[side] / len(TriadCardDB.mapRarity[i])

        print('Loaded cards:',len(TriadCardDB.cards))

    @staticmethod
    def find(name):
        for card in TriadCardDB.cards:
            if (card.name == name):
                return card
        return TriadCardDB.cards[0]


# load cards on startup
TriadCardDB.load()
