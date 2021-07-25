class Agent:
    def __init__(self, game):
        pass

    def findAction(self, game, state):
        raise RuntimeError()

    def findTrainingAction(self, game, state):
        raise RuntimeError()

    def onTrainingGameStart(self, game, playerId):
        pass

    def onTrainingGameEnd(self, game, playerId):
        pass

    def onTrainingStep(self, game, playerId, state, action, nextState, reward):
        pass

    def train(self):
        pass

    def save(self, name):
        pass

    def load(self, name):
        pass

    def getTrainingDetails(self):
        return {}

    def generateModelCode(self, name):
        pass
