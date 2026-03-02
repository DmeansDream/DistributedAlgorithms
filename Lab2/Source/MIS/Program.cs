using System.Data.Common;
using System.Net;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace MIS;

class Program
{
    static CanvasRoot canvasRoot;
    static OrderedDictionary<string, Node> nodes;

    static void Main(string[] args)
    {
        Int32.TryParse(Console.ReadLine(), out int treeID);

        nodes = new OrderedDictionary<string, Node>();

        ParseGraph(treeID);
        Node root = nodes.Values.FirstOrDefault();
        RunMIS(root);

        FindWinningNodes(root, false);
        foreach(var node in nodes.Values.Where(n => n.isSelected))
        {
            Console.WriteLine($"{node.text} : {node.id}");
        }


    }

    static void ParseGraph(int treeId)
    {
        var text = File.ReadAllText(@$"..\..\..\{treeId}.canvas");
        
        canvasRoot = JsonConvert.DeserializeObject<CanvasRoot>(text);

        foreach(var node in canvasRoot.nodes)
        {
            nodes.Add(node.id, new Node{id = node.id, text = node.text});
        }

        foreach(var edgeData in canvasRoot.edges)
        {
            if(nodes.TryGetValue(edgeData.fromNode, out Node fromN) &&
            nodes.TryGetValue(edgeData.toNode, out Node toN))
            {
                fromN.children.Add(toN);
            }
        }
    }

    static void RunMIS(Node node)
    {
        if (node == null) return;
        
        node.scoreIncluded = 1;
        node.scoreExcluded = 0;

        foreach(var child in node.children)
        {
            RunMIS(child);

            node.scoreIncluded += child.scoreExcluded;
            node.scoreExcluded += Math.Max(child.scoreExcluded, child.scoreIncluded);
        }
        Console.WriteLine($"{node.text} : Included: {node.scoreIncluded}, Excluded : {node.scoreExcluded}");
    }

    static void FindWinningNodes(Node node, bool parentIncluded)
    {
        if (node == null) return;

        if (parentIncluded) node.isSelected = false;
        else if(node.scoreIncluded >= node.scoreExcluded) 
        {
            node.isSelected = true;
        }
        else
        {
            node.isSelected = false;
        }

        foreach (var child in node.children)
        {
            FindWinningNodes(child, node.isSelected);
        }
    }


    public class CanvasRoot
    {
        public List<NodeData> nodes;
        public List<EdgeData> edges;
    }

    public class NodeData
    {
        public string id;
        public string text;
    }

    public class EdgeData
    {
        public string fromNode;
        public string toNode;
    }

    public class Node
    {
        public string id;
        public string text;
        public bool isSelected;
        public List<Node> children = new();
        public int scoreIncluded;
        public int scoreExcluded;
    }
}
