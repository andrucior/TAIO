import networkx as nx
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt

def draw_directed_graph_from_adjacency(matrix):
    # Create a directed graph
    G = nx.DiGraph()

    n = len(matrix)
    G.add_nodes_from(range(n))

    # Add edges where matrix[i][j] != 0
    for i in range(n):
        for j in range(n):  # check all pairs for directed edges
            if matrix[i][j] != 0:
                # If multigraph, you could add multiple edges
                G.add_edge(i, j)

    # Draw graph
    pos = nx.spring_layout(G, seed=42)
    nx.draw(G, pos, with_labels=True, node_color='lightgreen', node_size=800, font_size=12,
            arrows=True, arrowsize=20)
    plt.show()


# Example usage:
adj_matrix = [
    [0, 1, 0, 1],
    [0, 0, 1, 0],
    [0, 0, 0, 1],
    [0, 0, 0, 0]
]

draw_directed_graph_from_adjacency(adj_matrix)
