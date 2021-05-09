import struct
import base64

class GameGraph():
    def __init__(self):
        self.states = []
        self.transitions = []
        self.currentStateIdx = -1

    def storeMatchStart(self, stateArr):
        self.currentStateIdx = self.findOrAddState(stateArr)

    def storeMatchStep(self, stateArr):
        newStateIdx = self.findOrAddState(stateArr)
        self.transitions[self.currentStateIdx].append(newStateIdx)
        self.currentStateIdx = newStateIdx

    def findOrAddState(self, stateArr):
        stateStr = self.serializeState(stateArr)
        try:
            stateIdx = self.states.index(stateStr)
        except ValueError:
            stateIdx = len(self.states)
            self.states.append(stateStr)
            self.transitions.append([])

        return stateIdx

    def serializeState(self, stateArr):
        data = struct.pack('f'*len(stateArr), *stateArr)
        return base64.encodebytes(data)

    def descState(self):
        return "S:%i" % (len(self.states))
