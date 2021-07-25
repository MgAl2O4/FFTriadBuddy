import os
import math
import numpy as np
from .agent import Agent
from utils.trainingMemory import TrainingMemoryCircular
from utils.estimatorTorch import EstimatorModel

# Deep Q Network
#
class AgentDQN(Agent):
    def __init__(self, game):
        self.memorySize = 1 * 1000 * 1000
        self.batchSize = 256
        self.numLayersHidden = [500, 500]
        self.learningRate = 0.0001
        self.discountFactor = 0.99
        self.epsilonStart = 1
        self.epsilonEnd = 0.1
        self.epsilonDecay = 2000

        self.numActions = game.getMaxActions()
        numInputs = len(game.getState(0))

        self.estimatorQ = EstimatorModel(numInputs, self.numLayersHidden, self.numActions, self.learningRate)
        self.estimatorTarget = EstimatorModel(numInputs, self.numLayersHidden, self.numActions, self.learningRate)
        self.replayMemory = TrainingMemoryCircular(self.memorySize)
        self.epsilon = self.epsilonStart
        self.numSteps = 0
        self.historyLoss = []
        self.historyReward = []
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

    def onTrainingStep(self, game, playerId, state, action, nextState, reward):
        self.numSteps += 1
        self.epsilon = self.epsilonEnd + ((self.epsilonStart - self.epsilonEnd) * math.exp(-1.0 * self.numSteps / self.epsilonDecay))

        memorySample = (state, action, nextState, reward, game.isFinished())
        self.historyReward.append(reward)
        self.replayMemory.add(memorySample)
        if len(self.replayMemory) >= self.batchSize:
            self.trainOnBatch(game)

    def train(self):
        updateTargetCheckpoint = 'updateTarget.tmp'
        self.estimatorQ.save(updateTargetCheckpoint)
        self.estimatorTarget.load(updateTargetCheckpoint)
        os.remove(updateTargetCheckpoint)
        self.trainAvgLoss = np.average(self.historyLoss)
        self.trainAvgReward = np.average(self.historyReward)
        self.historyLoss = []
        self.historyReward = []

    def save(self, name):
        self.estimatorQ.save(name)

    def load(self, name):
        self.estimatorQ.load(name)
        self.estimatorTarget.load(name)

    def getTrainingDetails(self):
        return {
            'epsilon': self.epsilon,
            'memory': len(self.replayMemory) / self.replayMemory.capacity,
            'loss': self.trainAvgLoss,
            'reward': self.trainAvgReward,
        }

    def generateModelCode(self, name):
        self.estimatorQ.generateModelCode(name)

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
        predictedRewards = rewards + (np.invert(dones).astype(np.float) * self.discountFactor * nextTargets[np.arange(self.batchSize), bestActions])

        loss = self.estimatorQ.fit(states, actions, predictedRewards)
        self.historyLoss.append(loss)

        self.trainedOnce = True
