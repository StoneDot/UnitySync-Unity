using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAwaker : MonoBehaviour {

    public GameObject target;

	// Use this for initialization
	void Start () {
        var ins = UnitySync.GetInstance();
        Debug.Log(target);
        StartCoroutine(UpdateObject());
	}

    IEnumerator UpdateObject()
    {
        var ins = UnitySync.GetInstance();
        while(true)
        {
            ins.Client.PlaceObject(target);
            yield return new WaitForSeconds(5);
        }
    }
}
