from triadsession import TriadGameSession
from gamegraph import GameGraph
from tqdm import tqdm
import random


from dqn import Agent
import numpy as np
import tensorflow as tf

def OldStuff():
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
                batchSize=1024)

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

    #agent.save()
    print('Finished!')


def runExploration(numGames):
    history = GameGraph()
    metaHistory = GameGraph()

    print('Start exploration, numGame:%i' % (numGames))
    for i in tqdm(range(numGames)):
        seed = random.randrange(999999)
        random.seed(seed)
        session = TriadGameSession()
        session.seed = seed

        state, metaState = session.getState()
        history.storeMatchStart(state)
        metaHistory.storeMatchStart(metaState)

        while not session.isFinished():
            session.playRandomMove()

            state, metaState = session.getState()
            history.storeMatchStep(state)
            metaHistory.storeMatchStep(metaState)

    print('Exploration finished, history:%s, meta:%s' % (history.descState(), metaHistory.descState()))


if __name__ == "__main__":
    runExploration(100000)
