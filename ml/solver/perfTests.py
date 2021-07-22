import numpy as np
import random
from tqdm import tqdm

from gameSession import GameSession
from environment import Environment
from agents.agentRandom import AgentRandom
from agents.agentDQN import AgentDQN

def runTestPlayRandomGames(numGames, reproSeed):
    if reproSeed < 0:
        print('Running',numGames,'random test games...')

        for _ in tqdm(range(numGames)):
            seed = random.randrange(999999)
            random.seed(seed)
            np.random.seed(seed)

            session = GameSession()
            session.randomSeed = seed
            while not session.isFinished():
                playerId = session.getCurrentPlayer()
                state = session.getState(playerId)
                actions = session.getAllowedActions(state)
                action = random.choice(actions)
                session.step(action)
    else:
        random.seed(reproSeed)
        np.random.seed(reproSeed)

        session = GameSession()
        session.game.debug = True
        session.randomSeed = reproSeed

        print('DEBUG game')
        print('  mods:', [mod.name for mod in session.game.mods])
        print('  blue:', session.game.blueDeck)
        print('  red:', session.game.redDeck)

        while not session.isFinished():
            playerId = session.getCurrentPlayer()
            state = session.getState(playerId)
            actions = session.getAllowedActions(state)
            action = random.choice(actions)

            print('Move, player:%d numActions:%d (board:%d + card:%d) => action:%d (board:%d + card:%d)' %
                (playerId, len(actions),
                sum([ state[i] for i in range(9) ]),
                sum([ state[9 + i] for i in range(5) ]),
                action, action % 9, int(action / 9)))

            session.step(action)


def runTestAgentEval(numGames, agentName):
    print('Running',numGames,'eval games for agent',agentName)

    game = GameSession()
    evalAgent = AgentRandom(game)
    if agentName == 'dqn':
        evalAgent = AgentDQN(game)

    env = Environment(game, [evalAgent, AgentRandom(game)])

    score = 0
    for _ in tqdm(range(numGames)):
        rewards = env.runEvalGame()
        score += rewards[0]


if __name__ == "__main__":
    runTestPlayRandomGames(10000, reproSeed=-1)
    runTestAgentEval(10000, 'random')
    runTestAgentEval(10000, 'dqn')
