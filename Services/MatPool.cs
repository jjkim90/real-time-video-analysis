using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RealTimeVideoAnalysis.Services
{
    public sealed class MatPool : IDisposable
    {
        private readonly ConcurrentBag<Mat> _pool = new ConcurrentBag<Mat>();
        private readonly ConcurrentDictionary<Mat, DateTime> _rentedMats = new ConcurrentDictionary<Mat, DateTime>();
        private readonly Timer _cleanupTimer;
        private readonly object _statsLock = new object();
        
        // 풀 설정
        private const int MaxPoolSize = 50;
        private const int InitialPoolSize = 10;
        private const int CleanupIntervalSeconds = 30;
        private const int MaxRentDurationSeconds = 60;
        
        // 통계
        private long _totalRentCount;
        private long _totalReturnCount;
        private long _totalCreateCount;
        private long _totalDisposeCount;
        
        public MatPool()
        {
            // 초기 풀 생성
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _pool.Add(new Mat());
                Interlocked.Increment(ref _totalCreateCount);
            }
            
            // 주기적 정리 타이머
            _cleanupTimer = new Timer(Cleanup, null, 
                TimeSpan.FromSeconds(CleanupIntervalSeconds), 
                TimeSpan.FromSeconds(CleanupIntervalSeconds));
        }
        
        public Mat Rent()
        {
            Mat mat;
            
            if (_pool.TryTake(out mat))
            {
                // 풀에서 가져옴 - Release 호출하지 않음
                // Release()는 메모리를 해제하므로 재사용 시 문제 발생
            }
            else
            {
                // 풀이 비어있으면 새로 생성
                mat = new Mat();
                Interlocked.Increment(ref _totalCreateCount);
            }
            
            _rentedMats[mat] = DateTime.UtcNow;
            Interlocked.Increment(ref _totalRentCount);
            
            return mat;
        }
        
        public void Return(Mat mat)
        {
            if (mat == null || mat.IsDisposed)
                return;
                
            DateTime rentTime;
            if (!_rentedMats.TryRemove(mat, out rentTime))
            {
                // 이미 반환되었거나 풀에서 빌린 것이 아님
                return;
            }
            
            Interlocked.Increment(ref _totalReturnCount);
            
            // 풀 크기 제한 확인
            if (_pool.Count < MaxPoolSize && !mat.IsDisposed)
            {
                _pool.Add(mat);
            }
            else
            {
                // 풀이 가득 차면 dispose
                mat.Dispose();
                Interlocked.Increment(ref _totalDisposeCount);
            }
        }
        
        public Mat Rent(int rows, int cols, MatType type)
        {
            var mat = Rent();
            
            // 크기나 타입이 다르면 재생성
            // Create()는 기존 메모리를 재사용하거나 필요시 재할당
            if (mat.Rows != rows || mat.Cols != cols || mat.Type() != type)
            {
                mat.Create(rows, cols, type);
            }
            
            // 데이터 초기화 (이전 데이터 제거)
            mat.SetTo(Scalar.All(0));
            
            return mat;
        }
        
        public Mat RentLike(Mat template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
                
            return Rent(template.Rows, template.Cols, template.Type());
        }
        
        private void Cleanup(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // 오래 대여된 Mat 확인 (메모리 누수 방지)
                foreach (var kvp in _rentedMats)
                {
                    if ((now - kvp.Value).TotalSeconds > MaxRentDurationSeconds)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MatPool] 경고: Mat 객체가 {MaxRentDurationSeconds}초 이상 반환되지 않음");
                    }
                }
                
                // 풀 크기가 너무 크면 일부 제거
                while (_pool.Count > MaxPoolSize)
                {
                    Mat mat;
                    if (_pool.TryTake(out mat))
                    {
                        mat.Dispose();
                        Interlocked.Increment(ref _totalDisposeCount);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MatPool] Cleanup 오류: {ex.Message}");
            }
        }
        
        public string GetStats()
        {
            lock (_statsLock)
            {
                return $"[MatPool 통계] " +
                       $"풀 크기: {_pool.Count}, " +
                       $"대여 중: {_rentedMats.Count}, " +
                       $"총 대여: {_totalRentCount}, " +
                       $"총 반환: {_totalReturnCount}, " +
                       $"총 생성: {_totalCreateCount}, " +
                       $"총 삭제: {_totalDisposeCount}";
            }
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            
            // 모든 Mat 정리
            Mat mat;
            while (_pool.TryTake(out mat))
            {
                mat.Dispose();
            }
            
            foreach (var kvp in _rentedMats)
            {
                if (!kvp.Key.IsDisposed)
                {
                    kvp.Key.Dispose();
                }
            }
            
            _rentedMats.Clear();
        }
    }
    
    public struct PooledMat : IDisposable
    {
        private readonly MatPool _pool;
        private readonly Mat _mat;
        
        public Mat Mat => _mat;
        
        public PooledMat(MatPool pool, Mat mat)
        {
            _pool = pool;
            _mat = mat;
        }
        
        public void Dispose()
        {
            _pool?.Return(_mat);
        }
    }
    
    public static class MatPoolExtensions
    {
        public static PooledMat RentScoped(this MatPool pool)
        {
            return new PooledMat(pool, pool.Rent());
        }
        
        public static PooledMat RentScoped(this MatPool pool, int rows, int cols, MatType type)
        {
            return new PooledMat(pool, pool.Rent(rows, cols, type));
        }
        
        public static PooledMat RentScopedLike(this MatPool pool, Mat template)
        {
            return new PooledMat(pool, pool.RentLike(template));
        }
    }
}