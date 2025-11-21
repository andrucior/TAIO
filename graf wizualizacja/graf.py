import numpy as np
import networkx as nx
import matplotlib.pyplot as plt

# Read the file and parse first matrix
with open("graf.txt", "r") as f:
    lines = f.readlines()
    n = int(lines[0].strip())  # first line is size
    M = []
    for i in range(1, n+1):
        row = list(map(int, lines[i].split()))
        M.append(row)
    M = np.array(M)

G = nx.from_numpy_array(M, create_using=nx.DiGraph())
pos = nx.spring_layout(G, seed=42)
nx.draw(G, pos, with_labels=True, node_color='lightblue', 
        node_size=800, font_size=12, arrows=True, arrowsize=20)
plt.show()
