using UnityEngine;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class BackgroundTiler2D : MonoBehaviour
    {
        private readonly Transform[] _tiles = new Transform[9];
        private Transform _viewer;
        private SpriteRenderer _source;
        private Vector2 _tileSize;
        private Vector3 _origin;
        private bool _initialized;

        public void Bind(Transform viewer)
        {
            _viewer = viewer;
            if (_initialized)
                return;

            _source = GetComponent<SpriteRenderer>();
            if (_source == null || _source.sprite == null)
                return;

            _origin = transform.position;
            _tileSize = _source.bounds.size;
            if (_tileSize.x <= Mathf.Epsilon || _tileSize.y <= Mathf.Epsilon)
                return;

            _tiles[0] = transform;
            for (int i = 1; i < _tiles.Length; i++)
                _tiles[i] = CreateTile(i).transform;

            _initialized = true;
            RepositionTiles();
        }

        private GameObject CreateTile(int index)
        {
            GameObject tile = new GameObject($"BackgroundTile_{index}");
            tile.layer = gameObject.layer;
            tile.transform.SetParent(transform.parent, true);
            tile.transform.rotation = transform.rotation;
            tile.transform.localScale = transform.localScale;

            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = _source.sprite;
            renderer.sharedMaterial = _source.sharedMaterial;
            renderer.color = _source.color;
            renderer.flipX = _source.flipX;
            renderer.flipY = _source.flipY;
            renderer.sortingLayerID = _source.sortingLayerID;
            renderer.sortingOrder = _source.sortingOrder;
            renderer.maskInteraction = _source.maskInteraction;
            return tile;
        }

        private void LateUpdate()
        {
            if (_initialized && _viewer != null)
                RepositionTiles();
        }

        private void RepositionTiles()
        {
            int centerX = Mathf.RoundToInt((_viewer.position.x - _origin.x) / _tileSize.x);
            int centerY = Mathf.RoundToInt((_viewer.position.y - _origin.y) / _tileSize.y);
            int index = 0;

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3 position = _origin + new Vector3(
                        (centerX + x) * _tileSize.x,
                        (centerY + y) * _tileSize.y,
                        0f);
                    position.z = _origin.z;
                    _tiles[index++].position = position;
                }
            }
        }
    }
}
