import multiprocessing as mp
import pandas as pd
import json
import random

color_map = {1 : "red",
             2 : "orange",
             3 : "yellow",
             4 : "green",
             5 : "blue",
             6 : "violet"}

def parse_graph():
    f = open("color2.canvas")
    data = json.load(f)
    nodes = data['nodes']
    edges = data['edges']

    nodeid_map = {node['id'] : int(node['text']) for node in nodes}
    nodecolor_map = {int(node['text']) : int(node['color']) for node in nodes}
    edges_map = [{} for _ in range (len(nodeid_map) + 1)]

    def add_edge(u, v):
        if u not in edges_map[v]:
            edges_map[v][u] = 1

        if v not in edges_map[u]:
            edges_map[u][v] = 1

    for edge in edges:
        v_id = edge['fromNode']
        u_id = edge['toNode']

        v = nodeid_map.get(v_id, 0)
        u = nodeid_map.get(u_id, 0)
        add_edge(u,v)

    df = pd.DataFrame(edges_map).fillna("-")
    #print(df)
    return nodecolor_map, edges_map

def save_colored_graph(paint_dict):
    file = "result2.canvas"

    with open(file, 'r') as f:
        data = json.load(f)

    for node in data['nodes']:
        node_id = int(node['text'])

        if node_id in paint_dict:
            node['color'] = str(paint_dict[node_id])

    with open(file, 'w') as f:
        json.dump(data, f, indent=4)


def coloring_worker(id, vertices, U, R, Res, edges, barrier, minI):
    possible_colors = {1,2,3,4,5,6}
    while len(U) > 0:
        for v in vertices:
            if v not in U:
                continue

            available_colors = possible_colors.copy()

            for u in edges[v]:
                dis_col = U.get(u[0])
                if dis_col is None:
                    dis_col = Res.get(u[0])

                print(f"W:{id} ## edge:{v} - u:{u}, {dis_col}")
                available_colors.discard(dis_col)

            U[v] = min(available_colors)
            with minI.get_lock():
                minI.value += 1

        barrier.wait()

        if id == 0:
            for v in U.keys():
                Res[v] = U[v]

        barrier.wait()

        for v in vertices:
            if v not in U:
                continue
            for u in edges[v]:
                neigh_col = U.get(u[0])
                if neigh_col is None:
                    neigh_col = Res.get(u[0])
                    if U.get(v) == neigh_col and v < u[0]:
                        R[v] = U[v]
                else:
                    if U.get(v) == neigh_col and v < u[0]:
                        R[v] = U[v]


        barrier.wait()

        if id == 0:
            U.clear()
            U.update(R)
            R.clear()

        barrier.wait()


def initiation(V, nodecolor_map, edges):
    workers = 4
    paint = dict()
    minI = mp.Value('i', 0)

    with mp.Manager() as manager:
        U = manager.dict()
        R = manager.dict()
        Res = manager.dict()

        for i in range(V):
            U[i + 1] = nodecolor_map[i + 1]

        barrier = mp.Barrier(workers)
        processes = []

        all_vertices = list(range(1, V + 1))
        random.shuffle(all_vertices)
        part = V // workers

        for i in range(workers):
            if i == workers - 1:
                vertices = all_vertices[i * part : ]
            else:
                vertices = all_vertices[i * part : (i+1) * part]

            p = mp.Process(target=coloring_worker, args=(i, vertices, U, R, Res, edges, barrier, minI))
            processes.append(p)
            p.start()

        for p in processes:
            p.join()

        paint = dict(Res)
        count = minI.value

    print(paint)
    print(count)
    return paint


if __name__ == '__main__':

    nodecolor_map, edges_map = parse_graph()

    edges = []
    for rec in edges_map:
        edges.append(list(rec.items()))

    final_paint = initiation(len(nodecolor_map), nodecolor_map, edges)

    if final_paint:
        save_colored_graph(final_paint)

