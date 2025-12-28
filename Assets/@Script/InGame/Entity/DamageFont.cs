using MewVivor.Common;
using MewVivor.Managers;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace MewVivor.InGame.Entity
{
    public class DamageFont : MonoBehaviour
    {
        // 1. UI용 컴포넌트로 변경
        private TextMeshProUGUI _damageText;
        private RectTransform _rectTransform; // UI는 RectTransform 필수

        // 2. 컬러 캐싱
        private static readonly Color HealColor = Utils.HexToColor("4EEE6F");
        private static readonly Color CriticalColor = Utils.HexToColor("EFAD00");
        private static readonly Color NormalColor = Color.white;
        
        // 3. UI 이동 거리 설정 (Canvas 모드에 따라 1단위 크기가 다름)
        // World Space Canvas라면 1~2 정도, Screen Space라면 50~100 정도가 적당함
        [SerializeField] private float _floatDistance = 50f; 
        
        private void Awake()
        {
            // UI 버전 컴포넌트 가져오기
            _damageText = GetComponent<TextMeshProUGUI>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public void SetInfo(Vector2 pos, float damage = 0, float healAmount = 0, Transform parent = null, bool isCritical = false)
        {
            if (_damageText == null) TryGetComponent(out _damageText);
            if (_rectTransform == null) TryGetComponent(out _rectTransform);

            // 위치 설정 (UI 좌표계)
            // pos가 스크린 좌표인지 월드 좌표인지에 따라 변환이 필요할 수 있음.
            // 여기서는 이미 변환된 UI 좌표(anchoredPosition) 혹은 월드 캔버스의 position이라 가정합니다.
            _rectTransform.position = pos; 
            
            // 텍스트 및 컬러 설정
            if (healAmount > 0)
            {
                _damageText.text = Mathf.RoundToInt(healAmount).ToString();
                _damageText.color = HealColor;
            }
            else if (isCritical)
            {
                _damageText.text = Mathf.RoundToInt(damage).ToString();
                _damageText.color = CriticalColor;
            }
            else
            {
                _damageText.text = Mathf.RoundToInt(damage).ToString();
                _damageText.color = NormalColor;
            }

            _damageText.alpha = 1;

            // [UI 정렬] MeshRenderer의 SortingOrder 대신 Hierarchy 순서를 사용합니다.
            // 가장 마지막에 그려지게 하여 맨 위에 표시 (SetAsLastSibling)
            _rectTransform.SetAsLastSibling();
            
            gameObject.SetActive(true);
            DoAnimation();
        }

        private void DoAnimation()
        {
            _rectTransform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(_rectTransform.DOScale(1.1f, 0.3f).SetEase(Ease.InOutBounce))
                .Join(_rectTransform.DOMoveY(_rectTransform.position.y + _floatDistance, 0.3f).SetEase(Ease.Linear))
               .Append(_rectTransform.DOScale(1.0f, 0.3f).SetEase(Ease.InOutBounce))
               .Join(_damageText.DOFade(0, 0.3f).SetEase(Ease.InQuint)) 
               .OnComplete(OnAnimationComplete);
        }

        private void OnAnimationComplete()
        {
            gameObject.SetActive(false);
            // Manager.I.Pool.ReleaseObject(nameof(DamageFont), gameObject);
        }
        
        private void OnDisable()
        {
            _rectTransform.localScale = Vector3.one; 
            _damageText.alpha = 1f;
        }
    }
}