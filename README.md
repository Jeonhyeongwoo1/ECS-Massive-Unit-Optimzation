## 목차
1. [프로젝트 개요](#overview)
2. [Unity 내의 환경(Hybrid방식)](#env)
3. [소스 코드 링크](#code)
4. [트러블 슈팅](#trouble)[트러블 슈팅](#4-트러블-슈팅)
<a id="overview"></a>
## 프로젝트 개요

- 프로젝트 명 : ECS  Massive Unit Optimization
- 목표 :  기존 구조(GameObject 기반)의 구조 성능 한계를 극복하여 모바일 환경에서 쾌적환 환경 구축
- 기술 : Unity DOTS
- 성과 : 몬스터 3000 기준 20 FPS 이하 → 60 FPS 성능 향상
- 영상 : https://github.com/user-attachments/assets/3123807b-fa63-4948-8ecf-5bc18b551cd3
<br/>

<a id="env"></a>
## Unity 내의 환경(Hybrid방식)
<img width="1358" height="763" alt="image (1)" src="https://github.com/user-attachments/assets/0a30d61d-c8c4-46eb-95f9-a4883cae037e" />
<br/>

<a id="code"></a>
## 소스 코드 링크
  - IJobEntity를 활용한 스킬 충돌 로직
    - https://github.com/Jeonhyeongwoo1/ECS-Massive-Unit-Optimzation/blob/main/Assets/%40Script/InGame/ECS/Skill/System/SkillCollisionSystem.cs
  - 스킬 Hit 업데이트 로직
    - https://github.com/Jeonhyeongwoo1/ECS-Massive-Unit-Optimzation/blob/main/Assets/%40Script/InGame/ECS/Skill/System/SkillUpdateSystem.cs
  - 몬스터 충돌 로직
    - https://github.com/Jeonhyeongwoo1/ECS-Massive-Unit-Optimzation/blob/main/Assets/%40Script/InGame/ECS/Monster/System/MonsterCollisionSystem.cs
  - 몬스터 이동 로직
    - https://github.com/Jeonhyeongwoo1/ECS-Massive-Unit-Optimzation/blob/main/Assets/%40Script/InGame/ECS/Monster/System/MonsterMoveSystem.cs
<br/>

<a id="trouble"></a>
## 트러블 슈팅
### 1
  - 문제점
    -ITriggerEventsJob은 매 프레임 충돌 여부만 반환하는 Stateless(무상태) 구조, 이로 인해 Enter(진입), Stay(지속), Exit(이탈) 상태를 구분할 수 없어, 스킬의 피격 및 지속 데미지 로직 구현에 구조적 한계 발생.
  - 해결방법
    - 트리거 이벤트가 발생하는 지점에서 NativeParallelMultiHashMap에 충돌 대상과 스킬에 대해서 저장
    - DynamicBuffer에 충돌 대상이 있는지 확인 후에, 대상이 있다면 현재 프레임 카운터 값을 업데이트
    - 대상이 없다면 새롭게 충돌 대상을 DynamicBuffer에 저장후 Enter처리
    - DynamicBuffer의 루프를 돌면서 현재 프레임 카운터 값과 다른 엔티티가 있으면 Exit처리
  - 결과
    - ECS 환경에서도 객체 지향의 OnTrigger 계열 이벤트와 동일한 정확한 생명주기(Lifecycle) 제어 가능.
    - 다수의 적이 동시에 충돌하더라도 누락 없이 개별적인 상태 관리 및 데미지 연산 수행.
