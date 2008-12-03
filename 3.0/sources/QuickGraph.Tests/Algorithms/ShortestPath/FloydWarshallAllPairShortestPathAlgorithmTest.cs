﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph.Unit;
using QuickGraph.Algorithms.ShortestPath;
using QuickGraph.Collections;
using QuickGraph.Algorithms;
using QuickGraph.Serialization;
using QuickGraph.Algorithms.Observers;

namespace QuickGraph.Tests.Algorithms.ShortestPath
{
    [TestFixture]
    public class BoostFloydWarshallTest
    {
        public static AdjacencyGraph<char, Edge<char>> CreateGraph(Dictionary<Edge<char>, double> distances)
        {
            var g = new AdjacencyGraph<char, Edge<char>>();

            var vertices = "ABCDE";
            g.AddVertexRange(vertices);
            AddEdge(g, distances, 'A', 'C', 1);
            AddEdge(g, distances, 'B', 'B', 2);
            AddEdge(g, distances, 'B', 'D', 1);
            AddEdge(g, distances, 'B', 'E', 2);
            AddEdge(g, distances, 'C', 'B', 7);
            AddEdge(g, distances, 'C', 'D', 3);
            AddEdge(g, distances, 'D', 'E', 1);
            AddEdge(g, distances, 'E', 'A', 1);
            AddEdge(g, distances, 'E', 'B', 1);

            return g;
        }

        [Test]
        public void Compute()
        {
            var distances = new Dictionary<Edge<char>, double>();
            var g = CreateGraph(distances);
            var fw = new FloydWarshallAllShortestPathAlgorithm<char, Edge<char>>(g, e => distances[e]);
            fw.Compute();
            fw.Dump(Console.Out);
            foreach (var i in g.Vertices)
                foreach (var j in g.Vertices)
                {
                    Console.Write("{0} -> {1}:", i, j);
                    IEnumerable<Edge<char>> path;
                    if (fw.TryGetPath(i, j, out path))
                    {
                        double cost = 0;
                        foreach (var edge in path)
                        {
                            Console.Write("{0}, ", edge.Source);
                            cost += distances[edge];
                        }
                        Console.Write("{0} --- {1}", j, cost);
                    }
                    Console.WriteLine();
                }
            {
                double distance;
                Assert.IsTrue(fw.TryGetDistance('A', 'A', out distance));
                Assert.AreEqual(0, distance);

                Assert.IsTrue(fw.TryGetDistance('A', 'B', out distance));
                Assert.AreEqual(6, distance);

                Assert.IsTrue(fw.TryGetDistance('A', 'C', out distance));
                Assert.AreEqual(1, distance);

                Assert.IsTrue(fw.TryGetDistance('A', 'D', out distance));
                Assert.AreEqual(4, distance);

                Assert.IsTrue(fw.TryGetDistance('A', 'E', out distance));
                Assert.AreEqual(5, distance);
            }
        }

        private static void AddEdge(
            AdjacencyGraph<char, Edge<char>> g,
            Dictionary<Edge<char>, double> distances,
            char source, char target, double weight)
        {
            var ac = new Edge<char>(source, target); distances[ac] = weight; g.AddEdge(ac);
        }
    }

    [TestFixture]
    public class FloydDijkstraCompareTest
    {
        [Test]
        public void Boost()
        {
            var distances = new Dictionary<Edge<char>, double>();
            var g = BoostFloydWarshallTest.CreateGraph(distances);
            this.Compare(g, e => distances[e]);
        }

        [Test]
        public void GraphML()
        {
            Func<IdentifiableEdge<IdentifiableVertex>, double> distances = e => 1;
            foreach (var g in GraphMLFilesHelper.GetGraphs())
                this.Compare(g, distances);
        }

        [Test]
        public void G23103GraphML()
        {
            Func<IdentifiableEdge<IdentifiableVertex>, double> distances = e => 1;
            var g = GraphMLFilesHelper.LoadGraph(@"GraphML\g.23.103.graphml");
            Compare(g, distances);
        }

        //static Color ToColor(GraphColor color)
        //{
        //    switch (color)
        //    {
        //        case GraphColor.Gray:
        //            return Color.Gray;
        //        case GraphColor.Black:
        //            return Color.Black;
        //        default:
        //            return Color.White;
        //    }
        //}

        void Compare<TVertex, TEdge>(AdjacencyGraph<TVertex, TEdge> g, Func<TEdge, double> distances)
            where TEdge : IEdge<TVertex>
        {
            // compute all paths
            var fw = new FloydWarshallAllShortestPathAlgorithm<TVertex, TEdge>(g, distances);
            fw.Compute();
            var vertices = g.Vertices.ToArray();
            foreach (var source in g.Vertices)
            //var source = vertices[0];
            {
                var dijkstra = new DijkstraShortestPathAlgorithm<TVertex, TEdge>(g, distances);
                //dijkstra.ExamineEdge += (sender, e) =>
                //{
                //    MsaglGraphExtensions.ShowMsaglGraph(g,
                //        (s, ne) =>
                //        {
                //            ne.Node.Attr.FillColor = ToColor(dijkstra.VertexColors[ne.Vertex]);
                //        },
                //        (s, ne) =>
                //        {
                //            if (ne.Edge.Equals(e.Edge)) ne.GEdge.Attr.Color = Color.Red;
                //        });
                //};
                var predecessors = new VertexPredecessorRecorderObserver<TVertex, TEdge>();
                using (ObserverScope.Create(dijkstra, predecessors))
                    dijkstra.Compute(source);

                TryFunc<TVertex, IEnumerable<TEdge>> dijkstraPaths = predecessors.TryGetPath;
                //var target = vertices[20];
                foreach(var target in g.Vertices)
                {
                    if (source.Equals(target)) continue;

                    IEnumerable<TEdge> fwpath;
                    IEnumerable<TEdge> dijpath;
                    bool pathExists;
                    Assert.AreEqual(
                        pathExists = fw.TryGetPath(source, target, out fwpath),
                        dijkstraPaths(target, out dijpath));

                    if (pathExists)
                    {
                        var fwedges = fwpath.ToArray();
                        CheckPath<TVertex, TEdge>(source, target, fwedges);

                        var dijedges = dijpath.ToArray();
                        CheckPath<TVertex, TEdge>(source, target, dijedges);

                        // all distances are usually 1 in this test, so it should at least
                        // be the same number
                        if (dijedges.Length != fwedges.Length)
                        {
                            DumpPaths<TVertex, TEdge>(source, target, fwedges, dijedges);
                            Assert.Fail("path do not have the same length");
                        }

                        // check path length are the same
                        var fwlength = fwedges.Sum(distances);
                        var dijlength = dijedges.Sum(distances);
                        if (fwlength != dijlength)
                        {
                            DumpPaths<TVertex, TEdge>(source, target, fwedges, dijedges);
                            Assert.Fail("path do not have the same length");
                        }
                    }
                }
            }
        }

        private static void CheckPath<TVertex, TEdge>(TVertex source, TVertex target, TEdge[] fwedges) where TEdge : IEdge<TVertex>
        {
            Assert.IsTrue(fwedges[0].Source.Equals(source));
            for (int i = 0; i < fwedges.Length - 1; ++i)
                Assert.AreEqual(fwedges[i].Target, fwedges[i + 1].Source);
            Assert.IsTrue(fwedges[fwedges.Length - 1].Target.Equals(target));
        }

        private static void DumpPaths<TVertex, TEdge>(TVertex source, TVertex target, TEdge[] fwedges, TEdge[] dijedges) where TEdge : IEdge<TVertex>
        {
            Console.WriteLine("path: {0}->{1}", source, target);
            Console.WriteLine("dijkstra:");
            for (int j = 0; j < dijedges.Length; ++j)
                Console.WriteLine("\t{0}", dijedges[j]);
            Console.WriteLine("floyd:");
            for (int j = 0; j < fwedges.Length; ++j)
                Console.WriteLine("\t{0}", fwedges[j]);
        }
    }

}
