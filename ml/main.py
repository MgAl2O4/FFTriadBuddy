from dqn import Agent
from triadgame import TriadGameSession
import numpy as np
import tensorflow as tf

tf.compat.v1.disable_eager_execution()

lr = 0.001
numGames = 10000

session = TriadGameSession()
observation = session.getState()
scores = []

agent = Agent(gamma=0.99, lr=lr, epsilon=1.0, epsilonDec=0.0005,
              inputSize=[len(observation)],
              numActions=session.getMaxActions(),
              memSize=1000000,
              batchSize=64)

for i in range(numGames):
    done = False
    score = 0
    session = TriadGameSession()
    observation = session.getState()
    while not done:
        action = agent.chooseAction(observation)
        observationNext, reward, done = session.step(action)
        score += reward
        agent.store(observation, action, reward, observationNext, done)
        observation = observationNext
        agent.learn()

    scores.append(score)
    avgScore = np.mean(scores[-100:])
    print('game:', i,
          'score %.2f' % score,
          'avgScore %.2f' % avgScore,
          'epsilon %.2f' % agent.epsilon)

agent.save()
print('Finished!')
