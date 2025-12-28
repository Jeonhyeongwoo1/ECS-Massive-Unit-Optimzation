using System;
using System.Collections.Generic;
using MewVivor;
using MewVivor.InGame.Entity;
using UnityEngine;

public class UI_FontController : MonoBehaviour
{
    
    private List<DamageFont> _pooledCriticalDamageFontList = new List<DamageFont>();
    private List<DamageFont> _pooledDamageFontList = new ();

    [SerializeField] private int _poolCount = 100;
    
    private void Awake()
    {
        for (int i = 0; i < _poolCount; i++)
        {
           var obj =  Manager.I.Resource.Instantiate("CriticalDamageFont");
           var damageFont = obj.GetComponent<DamageFont>();
           obj.transform.SetParent(transform);
           obj.SetActive(false);
           _pooledCriticalDamageFontList.Add(damageFont);
        }

        for (int i = 0; i < _poolCount; i++)
        {
            var obj =  Manager.I.Resource.Instantiate("DamageFont");
            var damageFont = obj.GetComponent<DamageFont>();
            obj.transform.SetParent(transform);
            obj.SetActive(false);
            _pooledDamageFontList.Add(damageFont);
        }
    }

    public DamageFont GetDamageFont(bool isCritical)
    {
        if (isCritical)
        {
            foreach (DamageFont damageFont in _pooledCriticalDamageFontList)
            {
                if (!damageFont.gameObject.activeInHierarchy)
                {
                    return damageFont;
                }
            }
            
            for (int i = 0; i < _poolCount; i++)
            {
                var obj =  Manager.I.Resource.Instantiate("CriticalDamageFont");
                var damageFont = obj.GetComponent<DamageFont>();
                _pooledCriticalDamageFontList.Add(damageFont);
            }
        }
        else
        {
            foreach (DamageFont damageFont in _pooledDamageFontList)
            {
                if (!damageFont.gameObject.activeInHierarchy)
                {
                    return damageFont;
                }
            }
       
            for (int i = 0; i < _poolCount; i++)
            {
                var obj =  Manager.I.Resource.Instantiate("DamageFont");
                var damageFont = obj.GetComponent<DamageFont>();
                _pooledDamageFontList.Add(damageFont);
            }
        }

        return null;
    }
}