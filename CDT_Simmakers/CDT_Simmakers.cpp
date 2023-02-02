#include "CDT.h"
#include "CDT_Simmakers.h"

CDT::Triangulation<double> GetCDT(
    std::vector<Vector2D>& points,
    std::vector<Edge2i>& edges) {
    CDT::Triangulation<double> cdt;
    cdt.insertVertices(
        points.begin(),
        points.end(),
        [](const Vector2D& p) { return p.x; },
        [](const Vector2D& p) { return p.y; }
    );
    cdt.insertEdges(
        edges.begin(),
        edges.end(),
        [](const Edge2i& e) { return e.a; },
        [](const Edge2i& e) { return e.b; }
    );
    //cdt.conformToEdges(
    //    edges.begin(),
    //    edges.end(),
    //    [](const Edge2i& e) { return e.a; },
    //    [](const Edge2i& e) { return e.b; }
    //);
    cdt.eraseOuterTrianglesAndHoles();

    return cdt;
}

bool Triangulate(
    int verticesCount, Vector2D* vertices,
    int edgesCount, Edge2i* edges,
    int* triangleVerticesCount, Vector2D** triangleVertices,
    int* trianglesCount, TriangleVIndexes** triangles) {

    if (vertices == nullptr || edges == nullptr ||
        verticesCount <= 0 || edgesCount <= 0)
        return false;

    std::vector<Vector2D> sourceVertices;
    for (int i = 0; i < verticesCount; ++i) {
        sourceVertices.push_back(Vector2D(vertices[i].x, vertices[i].y));
    }
    std::vector<Edge2i> sourceEdges;
    for (int i = 0; i < edgesCount; ++i) {
        sourceEdges.push_back(Edge2i(edges[i].a, edges[i].b));
    }


    CDT::Triangulation<double> triangulation;
    try {
        triangulation = GetCDT(
            sourceVertices,
            sourceEdges);
    }
    catch (...) {
        return false;
    }

    //auto triangles = triangulation.triangles;
    auto triangulateVertices = triangulation.vertices;

    *triangleVerticesCount = (triangulation.vertices.size());
    *trianglesCount = (triangulation.triangles.size());

    auto triV = new Vector2D[*triangleVerticesCount];
    auto tri = new TriangleVIndexes[*trianglesCount];

    for (int i = 0; i < *triangleVerticesCount; ++i) {
        auto vtx = triangulateVertices[i];
        triV[i] = Vector2D(vtx.x,vtx.y);
    }
    for (int i = 0; i < *trianglesCount; ++i) {
        auto triangle = triangulation.triangles[i].vertices;
        tri[i] = TriangleVIndexes(triangle[0], triangle[1], triangle[2]);
    }

    *triangleVertices = triV;
    *triangles = tri;

    return true;
}