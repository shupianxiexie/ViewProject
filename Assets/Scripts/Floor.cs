using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using UnityEngine;

public class Floor : MonoBehaviour
{
    public Vector3 _Position, _Scale, _Rotation;
    public string _Name;
    public static List <Floor> allFloors = new List<Floor>();
    public void GetFloor()
    {
        _Name = gameObject.name;
        _Position = transform.localPosition;
        _Scale = transform.localScale;
        _Rotation = transform.localEulerAngles;
    }
    void Awake()
    {
        allFloors.Add(this);
        GetFloor();
    }
    void OnDestroy()
    {
        allFloors.Remove(this);
    }
}
