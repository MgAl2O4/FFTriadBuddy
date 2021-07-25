import os
import textwrap

class CodeGenerator():
    def __init__(self):
        self.layers = []

    def addLayer(self, weights, biases, activation):
        self.layers.append((weights, biases, activation))

    def save(self, name):
        lines = []
        lines.append('// Auto generated model data')
        lines.append('private void InitModel()')
        lines.append('{')

        numInputs = self.layers[0][0].shape[1]
        lines.append('\tNumInputs = %d;' % numInputs)

        numOutputs = [str(layer[0].shape[0]) for layer in self.layers]
        lines.append('\tNumOutputs = {%s};' % ', '.join(numOutputs))

        for i in range(len(self.layers)):
            lines.append('\tLayer%d_W = %s;' % (i, self.convertToCSArray(self.layers[i][0])))
            lines.append('\tLayer%d_B = %s;' % (i, self.convertToCSArray(self.layers[i][1])))

        lines.append('}')

        file = open(name, 'w')
        for line in lines:
            wrapLines = textwrap.wrap(line, width=120, tabsize=4, subsequent_indent='\t\t')
            for fileLine in wrapLines:
                file.write(fileLine + '\n')

        file.close()
        print('Saved generated code:',name)

    def convertToCSArray(self, arr):
        values = []
        if len(arr.shape) == 1:
            values = [str(x) for x in arr]
        else:
            for i in range(arr.shape[0]):
                values.append(self.convertToCSArray(arr[i]))

        return '{' + ', '.join(values) + '}'
