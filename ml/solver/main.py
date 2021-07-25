from gameSession import GameSession
from environment import Environment
from agents.agentRandom import AgentRandom
from agents.agentDQN import AgentDQN
from tqdm import tqdm
import matplotlib.pyplot as plt
import time

def trainModel():
    numEpochs = 20
    numGamesToTrain = 200
    numGamesToEval = 1000

    game = GameSession(useModifiers=True)
    trainingAgent = AgentDQN(game)
    randomAgent = AgentRandom(game)

    learningEnv = Environment(game, [trainingAgent, trainingAgent])
    evalEnv = Environment(game, [trainingAgent, randomAgent])

    # load checkpoint data
    #trainingAgent.load('data/model.tmp')

    print('Start training, epochs:',numEpochs)
    timeStart = time.time()
    history = []
    for epochIdx in range(numEpochs):
        # play a lot and learn and stuff
        for _ in tqdm(range(numGamesToTrain), leave=False, desc='Training'.ljust(15)):
            learningEnv.runTrainingGame()

        # training update after exploration step
        trainingAgent.train()

        # eval with same seed for every iteration
        randomAgent.setSeed(0)
        score = 0
        for _ in tqdm(range(numGamesToEval), leave=False, desc='Evaluating'.ljust(15)):
            rewards = evalEnv.runEvalGame()
            score += rewards[0]

        # collect info about training to see if it's actually working (lol, who am i kidding, it just fails constantly T.T)
        stepDetails = trainingAgent.getTrainingDetails()
        stepDetails['score'] = score

        history.append(stepDetails)
        print('[%d] %s' % (epochIdx, stepDetails))

    timeElapsed = time.time() - timeStart
    print('Done. Total time:%s, epoch avg:%.0fs' % (time.strftime("%H:%M:%Ss", time.gmtime(timeElapsed)), timeElapsed / numEpochs))

    trainingAgent.save('data/model.tmp')
    trainingAgent.generateModelCode('data/model.cs')
    return history

def showTrainingData(history):
    epochs = range(len(history))
    fig, axs = plt.subplots(2, sharex=True)
    axs[0].title.set_text('Reward')
    axs[0].plot(epochs, [stepDetails['reward'] for stepDetails in history])
    axs[1].title.set_text('Loss')
    axs[1].plot(epochs, [stepDetails['loss'] for stepDetails in history])
    plt.xlabel('Epochs')
    plt.show()

if __name__ == "__main__":
    history = trainModel()
    showTrainingData(history)
