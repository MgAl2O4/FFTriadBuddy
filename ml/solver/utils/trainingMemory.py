from collections import deque
import random

class TrainingMemory:
    def __init__(self, capacity):
        self.capacity = capacity
        self.clear()

    def add(self, sampleTuple):
        self.numWrites += 1
        if len(self) < self.capacity:
            self.buffer.append(sampleTuple)
            return True
        return False

    def clear(self):
        self.numWrites = 0
        self.buffer = deque([], maxlen=self.capacity)

    def __len__(self):
        return len(self.buffer)

    def sample(self, batchSize):
        samples = random.sample(self.buffer, batchSize)
        return zip(*samples)


class TrainingMemoryCircular(TrainingMemory):
    def add(self, sampleTuple):
        isAdded = super().add(sampleTuple)
        if not isAdded:
            writeIdx = self.numWrites % self.capacity
            self.buffer[writeIdx] = sampleTuple


class TrainingMemoryReservoir(TrainingMemory):
    def add(self, sampleTuple):
        isAdded = super().add(sampleTuple)
        if not isAdded:
            # Algorithm R
            writeIdx = random.randint(0, self.numWrites)
            if writeIdx < self.capacity:
                self.buffer[writeIdx] = sampleTuple
