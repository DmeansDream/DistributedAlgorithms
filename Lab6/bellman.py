import multiprocessing as mp
import json
import pandas as pd

def parse_graph():
    f = open("graph.canvas")
    data = json.load(f)
    nodes = data['nodes']
    edges = data['edges']

    node_map = {node['id'] : int(node['text']) for node in nodes}
    edges_map = [{} for _ in range (len(node_map) + 1)]

    def add_edge(u, v, weight):
        if u not in edges_map[v] or weight < edges_map[v][u]:
            edges_map[v][u] = weight

        if v not in edges_map[u] or weight < edges_map[u][v]:
            edges_map[u][v] = weight

    for edge in edges:
        v_id = edge['fromNode']
        u_id = edge['toNode']
        weight = int(edge['label'])

        v = node_map.get(v_id, 0)
        u = node_map.get(u_id, 0)
        add_edge(u,v,weight)
        print(v,":", u, "weight:", weight)

    df = pd.DataFrame(edges_map).fillna("-")
    print(df)
    return edges_map


def worker(id, start_v, end_v, V, distances_old, distances_new, edges, barrier):
    for step in range(V - 1):
        print(f"W : {id} step : {step}")
        for v in range(start_v, end_v):
            min_d = distances_old[v]

            for u, weight in edges[v]:
                if distances_old[u] == float('inf'):
                    continue
                if distances_old[u] + weight < min_d:
                    min_d = distances_old[u] + weight

            print(f"W : {id} - V :{v} ST:{start_v} TR:{end_v} MIN:{min_d}")
            distances_new[v] = min_d

        barrier.wait()

        if id == 0:
            for i in range(V):
                distances_old[i] = distances_new[i]

        barrier.wait()

def bellman_ford(V, edges, start_node):
    workers = 4
    distances_old = mp.Array('i', V)
    distances_new = mp.Array('i', V)

    for i in range(V):
        distances_old[i] = 1000000
        distances_new[i] = 1000000

    distances_old[start_node] = 0
    distances_new[start_node] = 0

    barrier = mp.Barrier(workers)
    processes = []

    part = V // workers

    for i in range(workers):
        start_v = i * part
        if i == workers - 1:
            end_v = V
        else:
            end_v = (i+1) * part

        p = mp.Process(target=worker, args=(i, start_v, end_v, V, distances_old, distances_new, edges, barrier))
        processes.append(p)
        p.start()

    for p in processes:
        p.join()

    return list(distances_old)


if __name__ == '__main__':
    edge_map = parse_graph()

    edges = []
    for rec in edge_map:
        edges.append(list(rec.items()))

    start_node = 11

    distances = bellman_ford(len(edge_map), edges, start_node)

    for i, d in enumerate(distances):
        print(f"Відстань від {i} : {d}")
