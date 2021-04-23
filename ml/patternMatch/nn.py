import tensorflow as tf
import numpy as np
import json
import textwrap

from tensorflow import keras
from tqdm.keras import TqdmCallback

class NNTraining():
    def __init__(self, inputFile, outputFile, labelKey="output"):
        path = 'data/'
        self.inputFile = path + inputFile
        self.outputFile = path + outputFile
        self.labelKey = labelKey
        pass
        
    def printToLines(self, prefix, values, suffix):
        longstr = prefix + ', '.join(('%ff' % v) for v in values) + suffix
        return textwrap.wrap(longstr, 250)

    def loadData(self):
        with open(self.inputFile) as file:
            training_sets = json.load(file)

        inputs = []
        outputs = []
        
        for elem in training_sets["dataset"]:
            inputs.append(elem["input"])
            outputs.append(elem[self.labelKey])

        return inputs, outputs
        

    def writeCodeFile(self, model, codeSuffix):
        lines = []

        for i in range(len(model.layers)):
            layer = model.layers[i]
            print('Layer[%i]:' % i)
            print('  input_shape:', layer.input_shape)
            print('  output_shape:', layer.output_shape)
            weights = layer.get_weights()
            for w in weights:
                print('  w.shape:', w.shape)
            print('  use_bias:', layer.use_bias)
            print('  activation:', layer.activation)

            if (len(weights) == 2 and layer.use_bias):
                listWeights = np.reshape(weights[0], -1)
                listBias = np.reshape(weights[1], -1)
                lines += self.printToLines('Layer%s%iW = new float[]{' % (codeSuffix, i), listWeights, '};')
                lines += self.printToLines('Layer%s%iB = new float[]{' % (codeSuffix, i), listBias, '};')

        with open(self.outputFile, "w") as file:
            for line in lines:
                file.write(line)
                file.write("\n")


    def run(self, numHidden1, numHidden2=0, numEpochs=20, batchSize=512, codeSuffix=''):
        x_train, y_train = self.loadData()
        x_train = np.array(x_train, np.float32)
        numClasses = max(y_train) + 1

        train_data = tf.data.Dataset.from_tensor_slices((x_train, y_train))
        train_data = train_data.repeat().shuffle(5000).batch(batchSize).prefetch(1)

        layers = []
        if (numHidden1 > 0):
            layers += [ tf.keras.layers.Dense(numHidden1, activation='relu') ]
        if (numHidden2 > 0):
            layers += [ tf.keras.layers.Dense(numHidden2, activation='relu') ]
        
        layers += [ tf.keras.layers.Dense(numClasses) ];
        model = tf.keras.Sequential(layers)
        model.compile(optimizer='adam',
                      loss=tf.keras.losses.SparseCategoricalCrossentropy(from_logits=True),
                      metrics=['accuracy'])

        model.fit(train_data,
          epochs=numEpochs,
          steps_per_epoch=100,
          verbose=0, callbacks=[TqdmCallback(verbose=2)])

        self.writeCodeFile(model, codeSuffix)
