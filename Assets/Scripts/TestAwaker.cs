using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAwaker : MonoBehaviour {

    public GameObject target;

	// Use this for initialization
	void Start () {
        var ins = UnitySync.GetInstance();
        Debug.Log(target);
        ins.Client.PlaceObject(target);
	}
}
