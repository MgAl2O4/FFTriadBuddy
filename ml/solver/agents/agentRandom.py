import numpy as np

class AgentRandom:
    def __init__(self, game):
        pass

    def findAction(self, game, state):
        actions = game.getAllowedActions(state)
        return np.random.choice(actions)

    def findTrainingAction(self, game, state):
        return self.findAction(game, state)

    def onTrainingGameStart(self, game, playerId):
        pass

    def onTrainingGameEnd(self, game, playerId):
        pass

    def onTrainingStep(self, game, playerId, state, action, nextState, reward):
        pass

    def Train(self):
        pass

    def Save(self, name):
        pass

    def Load(self, name):
        pass
