import os
import numpy as np
from utils.trainingMemory import TrainingMemoryCircular
from utils.estimatorTorch import EstimatorModel

class AgentDQN:
    def __init__(self, game):
        self.memorySize = 100 * 1000
        self.batchSize = 100
        self.numLayersHidden = [500, 500]
        self.learningRate = 0.001
        self.discountFactor = 0.99
        self.epsilonStart = 1
        self.epsilonEnd = 0.1
        self.epsilonDecrement = 0.001

        self.numActions = game.getMaxActions()
        numInputs = len(game.getState(0))

        self.estimatorQ = EstimatorModel(numInputs, self.numLayersHidden, self.numActions, self.learningRate)
        self.estimatorTarget = EstimatorModel(numInputs, self.numLayersHidden, self.numActions, self.learningRate)
        self.replayMemory = TrainingMemoryCircular(self.memorySize)
        self.epsilon = self.epsilonStart
        self.trainedOnce = False


    def findAction(self, game, state):
        probs = self.estimatorQ.predict(np.expand_dims(state, 0))[0]
        probs = self.sanitizeActions(game, state, probs)
        return np.argmax(probs)

    def findTrainingAction(self, game, state):
        useRandom = np.random.random() < self.epsilon
        if useRandom or not self.trainedOnce:
            allowedActions = game.getAllowedActions(state)
            return np.random.choice(allowedActions)

        return self.findAction(game, state)

    def onTrainingGameStart(self, game, playerId):
        pass

    def onTrainingGameEnd(self, game, playerId):
        pass

    def onTrainingStep(self, game, playerId, state, action, nextState, reward):
        self.epsilon = max(self.epsilonEnd, self.epsilon - self.epsilonDecrement)

        memorySample = (state, action, nextState, reward, game.isFinished())
        self.replayMemory.add(memorySample)
        if len(self.replayMemory) >= self.batchSize:
            self.trainOnBatch(game)

    def Train(self):
        updateTargetCheckpoint = 'updateTarget.tmp'
        self.estimatorQ.save(updateTargetCheckpoint)
        self.estimatorTarget.load(updateTargetCheckpoint)
        os.remove(updateTargetCheckpoint)

    def Save(self, name):
        self.estimatorQ.save(name)

    def Load(self, name):
        self.estimatorQ.load(name)
        self.estimatorTarget.load(name)

    def sanitizeActions(self, game, state, actionValues, badValue = -np.inf):
        maskedValues = badValue * np.ones(self.numActions, dtype=float)
        allowedActions = game.getAllowedActions(state)
        maskedValues[allowedActions] = actionValues[allowedActions]
        return maskedValues

    def trainOnBatch(self, game):
        states, actions, nextStates, rewards, dones = self.replayMemory.sample(self.batchSize)

        states = np.array(states)
        targets = self.estimatorTarget.predict(states)
        for i in range(self.batchSize):
            targets[i] = self.sanitizeActions(game, states[i], targets[i], badValue=-1)

        bestActions = np.argmax(targets, axis=1)
        nextTargets = self.estimatorTarget.predict(nextStates)
        updateTarget = rewards + np.invert(dones).astype(np.float) * self.discountFactor * nextTargets[np.arange(self.batchSize), bestActions]

        self.estimatorQ.fit(states, actions, updateTarget)
        self.trainedOnce = True
