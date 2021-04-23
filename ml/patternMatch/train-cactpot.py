from nn import NNTraining

training = NNTraining(inputFile='ml-cactpot.json', outputFile='ml-cactpot.txt')
training.run(numHidden1=80, numEpochs=500)
