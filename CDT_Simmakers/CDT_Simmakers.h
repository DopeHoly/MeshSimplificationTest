#pragma once


#define CDT_Simmakers_API __declspec(dllexport)

struct Vector2D {
    double x;
    double y;
    Vector2D() {
        x = 0;
        y = 0;
    }
    Vector2D(double X, double Y) {
        x = X;
        y = Y;
    }
};

struct Edge2i
{
    int a;
    int b;
    Edge2i(int A, int B) {
        a = A;
        b = B;
    }
};

struct TriangleVIndexes
{
    int a;
    int b;
    int c;
    TriangleVIndexes() {
        a = -1;
        b = -1;
        c = -1;
    }
    TriangleVIndexes(int A, int B, int C) {
        a = A;
        b = B;
        c = C;
    }
};

struct TriangleData {
    size_t vCount;
    size_t tCount;
    Vector2D* vertices;
    TriangleVIndexes* triangles;
};

//extern CDT_Simmakers_API bool Triangulate(int verticesCount, int edgesCount, MYPOINT* vertices, MYEDGE* edges);
extern "C" CDT_Simmakers_API bool Triangulate(
    int verticesCount, Vector2D * vertices,
    int edgesCount, Edge2i * edges,
    int* triangleVerticesCount, Vector2D ** triangleVertices,
    int* trianglesCount, TriangleVIndexes ** triangles);
