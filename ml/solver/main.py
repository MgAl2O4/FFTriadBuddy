from gameSession import GameSession
from environment import Environment
from agents.agentRandom import AgentRandom
from agents.agentDQN import AgentDQN
from tqdm import tqdm

def trainModel():
    numIters = 100
    numGamesToTrain = 100
    numGamesToEval = 1000

    game = GameSession()
    trainingAgent = AgentDQN(game)

    learningEnv = Environment(game, [trainingAgent, trainingAgent])
    evalEnv = Environment(game, [trainingAgent, AgentRandom(game)])

    for iter in range(numIters):
        print('[%d] Starting self play...' % iter)
        for _ in tqdm(range(numGamesToTrain)):
            learningEnv.runTrainingGame()

        trainingAgent.Train()

        print('[%d] Evaluating...' % iter)
        score = 0
        for _ in tqdm(range(numGamesToEval)):
            rewards = evalEnv.runEvalGame()
            score += rewards[0]

        print('[%d] Score: %f' % (iter, score))

if __name__ == "__main__":
    trainModel()
