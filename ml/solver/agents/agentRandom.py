import random
from .agent import Agent

class AgentRandom(Agent):
    def __init__(self, game):
        self.randGen = random.Random()

    def setSeed(self, seedValue):
        self.randGen = random.Random(seedValue)

    def findAction(self, game, state):
        actions = game.getAllowedActions(state)
        return self.randGen.choice(actions)

    def findTrainingAction(self, game, state):
        return self.findAction(game, state)
