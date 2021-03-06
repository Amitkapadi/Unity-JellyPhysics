﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Poly2Tri;

public class MeshBuilder : MonoBehaviour
{
    private const int MINIMUM_POINTS_FOR_MESH = 4;
    private const int MINIMUM_SIZE_FOR_LOOP = 10;
    private const int MINIMUM_ALLOWED_VERTICES = 4;

    public Material material;
    public Color vertexColor;
    private static MeshBuilder _instance;
    private Shape _lastCreatedObject;
    private float _meshDepth;
    private Polygon _polygon;
    private Vector2 _lowerBound;
    private Vector2 _upperBound;
    private Vector2 _boundingBox;
    private Vector2 _uvBounds;
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private Vector3[] _normals;
    private Color32[] _colors;
    private int[] _tris;
    private int _sortOrder;
    private List<Vector2> _points;
    private bool _filter;
    private Action<Shape> _finishedAction;
    private bool _updateExistingMesh;
    private Mesh _otherMesh;
//    private List<float> _normalPoints;

    public static MeshBuilder Instance
    {
        get{
            if(_instance == null)
            {
                _instance = GameObject.FindObjectOfType(typeof(MeshBuilder)) as MeshBuilder;
                if(_instance == null)
                {
                GameObject go = new GameObject();
                    go.name = "MeshBuilder";
                _instance = go.AddComponent<MeshBuilder>();
                }
            }
            return _instance;
        }
    }

    public void BuildMesh2D(List<Vector2> points, Action<Shape> completedHandler, bool filter = true)
    {
        _meshDepth = 0;
        _filter = filter;
        _updateExistingMesh = false;
        _finishedAction = completedHandler;
        BuildMesh(points);
    }

    private void BuildMesh(List<Vector2> points)
    {
        _lastCreatedObject = null;

        Vector2[] temp = new Vector2[points.Count];
        points.CopyTo(temp);
        _points = new List<Vector2>(temp);

        if (_points.Count < MINIMUM_ALLOWED_VERTICES)
        {
            InvalidShapeCreated();
            return;
        }

        if(_filter)
        {
            _points = LineFilter.Filter(_points);
        }

        ConstructPolygon();
    }

    private Transform _baseObject;
    public void UpdateMeshPoints(Mesh mesh,List<Vector2> points, bool filter = true)
    {
        _otherMesh = mesh;
        _meshDepth = 0;
        _filter = filter;
        _updateExistingMesh = true;
        BuildMesh(points);
    }

    private void ContinueCreatingShape()
    {
        AssignBoundsToPolygon();
       
        ConstructMeshData();
        if (_updateExistingMesh)
        {
            UpdateExisitingMesh();
        }
        else
        {
            AssignDataToMesh();
        
        if (_points.Count < MINIMUM_POINTS_FOR_MESH)
        {
            InvalidShapeCreated();
        }

        _finishedAction(_lastCreatedObject);
        }
    }


    private void InvalidShapeCreated()
    {
        if(_lastCreatedObject != null)
        {
            Destroy(_lastCreatedObject.BuiltGameObject);
        }
        _lastCreatedObject = null;
        throw new System.InvalidOperationException("Constructed points were invalid.");
    }

    private void ConstructPolygon()
    {
        List<PolygonPoint> p2 = new List<PolygonPoint>();
        int i = 0, l = _points.Count;
        for (; i < l; i += 1)
        {
            p2.Add(new PolygonPoint(_points[i].x, _points[i].y));
        }

        _polygon = new Polygon(p2);
        P2T.Triangulate(_polygon);

//        _loom.QueueOnMainThread(ContinueCreatingShape);
        ContinueCreatingShape();
    }

    private void AssignBoundsToPolygon()
    {
        _lowerBound = new Vector2((float)_polygon.MinX, (float)_polygon.MinY);
        _upperBound = new Vector2((float)_polygon.MaxX, (float)_polygon.MaxY);

        _boundingBox = _upperBound - _lowerBound;
//        Debug.Log(_boundingBox);

        _uvBounds = _boundingBox;
        if (_uvBounds.x > _uvBounds.y)
        {
            _uvBounds.y = _uvBounds.x;
        }
        else
        {
            _uvBounds.x = _uvBounds.y;
        }
//
//        if (_uvBounds.x > 4)
//        {
//            _uvBounds.x = _uvBounds.y = 4;
//        }
//        else
//        {
//            _uvBounds.x = _uvBounds.y = 4;
//        }
    }

    private void ConstructMeshData()
    {
        int vertCount = (_polygon.Triangles.Count * 3);
        int triCount = (_polygon.Triangles.Count * 3);

        _vertices = new Vector3[vertCount];
        _colors = new Color32[_vertices.Length];
        _uvs = new Vector2[_vertices.Length];
        _tris = new int[triCount];
        int i = 0;
        int j = (_polygon.Triangles.Count * 3);


        foreach (DelaunayTriangle triangle in _polygon.Triangles)
        {
            foreach (TriangulationPoint tp in triangle.Points)
            {
                _vertices[i] = new Vector3(tp.Xf, tp.Yf, 0);
               
                _colors[i] = vertexColor;

                Vector2 relativePoint = new Vector2(tp.Xf, tp.Yf) - _lowerBound;
                relativePoint = transform.TransformPoint(relativePoint);
                _uvs[i] = new Vector2(relativePoint.x / _boundingBox.x, relativePoint.y / _boundingBox.y);
//                Debug.Log(_uvs[i]);

                i++;
                j++;
            }
        }
        i = 0;
        j = 0;
        int l = _polygon.Triangles.Count;
        int count_tris = (_polygon.Triangles.Count * 3);


        //building front and back faces.
        for(;i<l;++i)
        {
            _tris[j] = j + 2;
            _tris[j + 1] = j + 1;
            _tris[j + 2] = j;

            j += 3;
        }

    }

    private void UpdateExisitingMesh()
    {
        _otherMesh.Clear();
        _otherMesh.vertices = _vertices;
//        _otherMesh.colors32 = _colors;
        _otherMesh.uv = _uvs;
        _otherMesh.triangles = _tris;
    }

    private void AssignDataToMesh()
    {
        Mesh msh = new Mesh();
        msh.vertices = _vertices;
        msh.uv = _uvs;
        msh.colors32 = _colors;
        msh.triangles = _tris;
        msh.RecalculateNormals();
        msh.Optimize();
        msh.RecalculateBounds();
        GameObject go = new GameObject();
        MeshFilter filter = go.AddComponent<MeshFilter>();
       go.AddComponent<MeshRenderer>().material = material;
        //go.transform.name = "DrawnObject";
        filter.mesh = msh;
        msh.name = "DrawnObjectMesh";

        Shape shape = go.AddComponent<Shape>();
        shape.BuiltGameObject = go;
        shape.BoundingBox = _boundingBox;
        shape.UVBounds = _uvBounds;
        shape.Area = _boundingBox.x * _boundingBox.y;
        shape.Points = _points.ToArray();
        shape.Polygon = _polygon;

        _lastCreatedObject = shape;
    }
}