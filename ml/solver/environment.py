class Environment(object):
    def __init__(self, game, playerAgents):
        self.game = game
        self.players = playerAgents

    def runTrainingGame(self):
        self.game.init()

        for idx in range(len(self.players)):
            self.players[idx].onTrainingGameStart(self.game, idx)

        while not self.game.isFinished():
            playerId = self.game.getCurrentPlayer()
            state = self.game.getState(playerId)

            action = self.players[playerId].findTrainingAction(self.game, state)
            self.game.step(action)

            # TODO: reuse nextState in next step, it contains relative data relative to player though (board owner, card visibility)
            nextState = self.game.getState(playerId)
            reward = self.game.getReward(playerId)
            self.players[playerId].onTrainingStep(self.game, playerId, state, action, nextState, reward)

        for idx in range(len(self.players)):
            self.players[idx].onTrainingGameEnd(self.game, idx)

    def runEvalGame(self):
        self.game.init()

        while not self.game.isFinished():
            playerId = self.game.getCurrentPlayer()
            state = self.game.getState(playerId)

            action = self.players[playerId].findAction(self.game, state)
            self.game.step(action)

        playerRewards = []
        for idx in range(len(self.players)):
            playerRewards.append(self.game.getReward(idx))

        return playerRewards
