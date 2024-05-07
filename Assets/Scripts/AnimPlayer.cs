using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimPlayer : MonoBehaviour
{
    private GPUSkinningPlayer _gpuSkinningPlayer;
    
    // Start is called before the first frame update
    void Start()
    {
        _gpuSkinningPlayer = GetComponent<GPUSkinningPlayerMono>().Player;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            //_gpuSkinningPlayer.Play("a_stand");
            //_gpuSkinningPlayer.CrossFade("a_stand", 0.5f);
            _gpuSkinningPlayer.CrossFade("Idle", 0.5f);
                
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            //_gpuSkinningPlayer.Play("a_attack");
            //_gpuSkinningPlayer.CrossFade("a_attack", 0.5f);
            _gpuSkinningPlayer.CrossFade("PlantNTurneft90", 0.5f);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            //_gpuSkinningPlayer.Play("a_move");
            //_gpuSkinningPlayer.CrossFade("a_move", 0.5f);
            _gpuSkinningPlayer.CrossFade("Run", 0.5f);
        }
    }
}
