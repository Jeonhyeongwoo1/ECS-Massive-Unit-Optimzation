using System;
using MewVivor.Enum;
using MewVivor.Factory;
using MewVivor.Managers;
using MewVivor.Model;
using MewVivor.Presenter;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MewVivor.Controller
{
    public class GameSceneController : BaseSceneController
    {
        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                var requestEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(requestEntity, new MonsterSpawnRequestComponent()
                {
                    Count = 1,
                    PlayerPosition = new float3(0,0,0),
                    Scale = 2.5f,
                    Speed = 2,
                    Radius = 2,
                    Atk = 10,
                    MaxHP = 130,
                    MonsterType = MonsterType.Normal,
                    SpawnedWaveIndex = 1
                });
            }
        }

        private void Initialize()
        {
            var playerModel = ModelFactory.CreateOrGetModel<PlayerModel>();
            var pausePopupPresenter = PresenterFactory.CreateOrGet<PausePopupPresenter>();
            pausePopupPresenter.Initialize(playerModel);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1;
        }
    }
}