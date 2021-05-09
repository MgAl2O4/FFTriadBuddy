# https://www.youtube.com/watch?v=SMZfgeHFFcA

import numpy as np
import tensorflow as tf
from tensorflow import keras
from tensorflow.keras.optimizers import Adam
from tensorflow.keras.models import load_model

class ReplayMemory():
    def __init__(self, maxSize, inputSize):
        self.maxSize = maxSize
        self.numWrites = 0
        self.state = np.zeros((maxSize, *inputSize), dtype=np.float32)
        self.nextState = np.zeros((maxSize, *inputSize), dtype=np.float32)
        self.action = np.zeros(self.maxSize, dtype=np.int32)
        self.reward = np.zeros(self.maxSize, dtype=np.float32)
        self.terminal = np.zeros(self.maxSize, dtype=np.int32)

    def store(self, state, action, reward, nextState, done):
        idx = self.numWrites % self.maxSize
        self.state[idx] = state
        self.action[idx] = action
        self.reward[idx] = reward
        self.nextState[idx] = nextState
        self.terminal[idx] = 1 - int(done)
        self.numWrites += 1
    
    def sample(self, batchSize):
        srcSize = min(self.numWrites, self.maxSize)
        indices = np.random.choice(srcSize, batchSize, False)

        states = self.state[indices]
        nextStates = self.nextState[indices]
        actions = self.action[indices]
        rewards = self.reward[indices]
        terminal = self.terminal[indices]
        return states, actions, rewards, nextStates, terminal

def buildDQN(lr, numActions, inputSize, fc1, fc2):
    model = keras.Sequential([
        keras.layers.Dense(fc1, activation='relu'),
        keras.layers.Dense(fc2, activation='relu'),
        keras.layers.Dense(numActions, activation=None)])
    model.compile(optimizer=Adam(learning_rate=lr), loss='mean_squared_error')
    return model

class Agent():
    def __init__(self, lr, gamma, numActions, epsilon, batchSize, inputSize,
                 epsilonDec=0.001, epsilonMin=0.01,
                 memSize=1000000, fname='dqn_model.stuff'):
        self.actionSpace = [i for i in range(numActions)]
        self.gamma = gamma
        self.epsilon = epsilon
        self.epsilonDec = epsilonDec
        self.epsilonMin = epsilonMin
        self.batchSize = batchSize
        self.fileName = fname
        self.memory = ReplayMemory(memSize, inputSize)
        self.q_eval = buildDQN(lr, numActions, inputSize, 256, 256)

    def store(self, state, action, reward, nextState, done):
        self.memory.store(state, action, reward, nextState, done)

    def chooseAction(self, observation):
        if (np.random.random() < self.epsilon):
            action = np.random.choice(self.actionSpace)
        else:
            state = np.array([observation])
            actions = self.q_eval.predict(state)
            action = np.argmax(actions)

        return action


    def learn(self):
        if (self.memory.numWrites < self.batchSize):
            return

        states, actions, rewards, nextStates, terminal = self.memory.sample(self.batchSize)

        q_eval = self.q_eval.predict(states)
        q_next = self.q_eval.predict(nextStates)

        q_target = np.copy(q_eval)
        indices = np.arange(self.batchSize, dtype=np.int32)
        q_target[indices, actions] = rewards + (self.gamma * np.max(q_next, axis=1) * terminal)

        self.q_eval.train_on_batch(states, q_target)
        self.epsilon = (self.epsilon - self.epsilonDec) if (self.epsilon > self.epsilonMin) else self.epsilonMin


    def save(self):
        self.q_eval.save(self.fileName)

    def load(self):
        self.q_eval = load_model(self.fileName)
