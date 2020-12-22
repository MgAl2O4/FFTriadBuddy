from dqn import Agent
import numpy as np
import textwrap

def printToLines(prefix, values, suffix):
    longstr = prefix + ', '.join(str(v) for v in values) + suffix
    return textwrap.wrap(longstr, 250)

print('Loading stored model...')
agent = Agent(0, 0, 0, 0, 0, [0])
agent.load()

lines = []

for i in range(len(agent.q_eval.layers)):
    layer = agent.q_eval.layers[i]
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
        lines += printToLines('Layer%iW = new float[]{' % i, listWeights, '};')
        lines += printToLines('Layer%iB = new float[]{' % i, listBias, '};')

file = open('model.cs', 'w')
for line in lines:
    file.write(line)
    file.write('\n')    
file.close()
print('Code patch saved!')
