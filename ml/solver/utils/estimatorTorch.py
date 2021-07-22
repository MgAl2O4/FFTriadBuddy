import numpy as np
import torch
import torch.nn as nn

torchDevice = torch.device("cuda" if torch.cuda.is_available() else "cpu")

class EstimatorNN(nn.Module):
    def __init__(self, numInputs, numHiddenArr, numOutputs):
        super(EstimatorNN, self).__init__()

        layerSizes = [ numInputs ] + numHiddenArr
        modelArgs = [nn.Flatten()]
        for i in range(len(layerSizes) - 1):
            modelArgs.append(nn.Linear(layerSizes[i], layerSizes[i + 1]))
            modelArgs.append(nn.ReLU())
        modelArgs.append(nn.Linear(layerSizes[-1], numOutputs))

        self.model = nn.Sequential(*modelArgs)

    def forward(self, s):
        return self.model(s)

class EstimatorModel:
    def __init__(self, numInputs, numHiddenArr, numOutput, learningRate):
        self.nn = EstimatorNN(numInputs, numHiddenArr, numOutput).to(torchDevice)
        self.nn.eval()
        self.lossFunc = nn.MSELoss(reduction='mean')
        self.optimizer = torch.optim.Adam(self.nn.parameters(), lr=learningRate)

    def predict(self, input):
        with torch.no_grad():
            input = torch.tensor(input).float().to(torchDevice)
            return self.nn(input).cpu().numpy()

    def fit(self, inputS, inputA, targetAR):
        self.nn.train()

        inputS = torch.tensor(inputS).float().to(torchDevice)
        inputA = torch.tensor(inputA).long().to(torchDevice)
        targetAR = torch.tensor(targetAR).float().to(torchDevice)

        outputAProbs = self.nn(inputS)
        outputAR = torch.gather(outputAProbs, dim=-1, index=inputA.unsqueeze(-1)).squeeze(-1)
        loss = self.lossFunc(outputAR, targetAR)

        self.optimizer.zero_grad()
        loss.backward()
        self.optimizer.step()
        self.nn.eval()

    def save(self, name):
        torch.save(self.nn.state_dict(), name)

    def load(self, name):
        self.nn.load_state_dict(torch.load(name))
        self.nn.eval()
