from nn import NNTraining

training = NNTraining(inputFile='ml-triad.json', outputFile='ml-triad.txt')
training.run(numHidden1=64, numEpochs=200)
