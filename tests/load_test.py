"""Нагрузочные GET-запросы к собственному стенду ГОЧС с авторизацией оператора."""
import argparse
import getpass
import http.cookiejar
import json
import pathlib
import statistics
import time
import urllib.error
import urllib.request
from collections import Counter
from concurrent.futures import ThreadPoolExecutor


def percentile(values, percent):
    values = sorted(values)
    if not values:
        return 0.0
    pos = (len(values) - 1) * percent / 100
    lo, hi = int(pos), min(len(values) - 1, int(pos) + 1)
    return values[lo] + (values[hi] - values[lo]) * (pos - lo)


def authenticate(base, password):
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    with opener.open(base + '/api/auth/token', timeout=10) as response:
        token = json.load(response)['token']
    req = urllib.request.Request(
        base + '/api/auth/login',
        data=json.dumps({'name': 'Нагрузочная проверка', 'password': password}).encode('utf-8'),
        headers={'Content-Type': 'application/json', 'X-CSRF-TOKEN': token},
        method='POST',
    )
    with opener.open(req, timeout=10) as response:
        if response.status != 200:
            raise RuntimeError('Не удалось войти в приложение')
    cookie = '; '.join(f'{item.name}={item.value}' for item in jar)
    if 'Gochs.Session=' not in cookie:
        raise RuntimeError('После входа не получен cookie сеанса')
    return cookie


def request_once(url, cookie, timeout):
    start = time.perf_counter()
    req = urllib.request.Request(url, headers={'Cookie': cookie})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            response.read()
            status = response.status
    except urllib.error.HTTPError as e:
        status = e.code
        e.close()
    except (OSError, TimeoutError):
        status = 0
    return status, (time.perf_counter() - start) * 1000


def main():
    parser = argparse.ArgumentParser(description='Нагрузочная проверка API ГОЧС')
    parser.add_argument('--base-url', default='http://127.0.0.1:5080')
    parser.add_argument('--endpoint', default='/api/dashboard')
    parser.add_argument('--requests', type=int, default=100)
    parser.add_argument('--workers', type=int, default=10)
    parser.add_argument('--timeout', type=float, default=15)
    parser.add_argument('--output', default='')
    args = parser.parse_args()
    if not 1 <= args.requests <= 10000 or not 1 <= args.workers <= 100:
        parser.error('requests: 1..10000, workers: 1..100')
    if not args.endpoint.startswith('/api/') or any(c in args.endpoint for c in '\r\n'):
        parser.error('Укажите маршрут чтения, например /api/dashboard')
    base = args.base_url.rstrip('/')
    if not base.startswith(('http://127.0.0.1:', 'http://localhost:')):
        parser.error('Для защиты от случайной нагрузки поддерживается только локальный сервер.')
    password = getpass.getpass('Пароль оператора (не отображается): ')
    cookie = authenticate(base, password)
    url = base + args.endpoint
    print(f'Тест: {args.requests} запросов, {args.workers} потоков, GET {args.endpoint}', flush=True)
    started = time.perf_counter()
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        rows = list(pool.map(lambda _: request_once(url, cookie, args.timeout), range(args.requests)))
    elapsed = time.perf_counter() - started
    statuses = Counter(code for code, _ in rows)
    latencies = [delay for _, delay in rows]
    success = sum(n for code, n in statuses.items() if 200 <= code < 300)
    report = {
        'url': url,
        'requests': args.requests,
        'workers': args.workers,
        'success': success,
        'errors': args.requests - success,
        'http_statuses': {str(code): n for code, n in sorted(statuses.items())},
        'total_seconds': round(elapsed, 3),
        'requests_per_second': round(args.requests / elapsed, 2) if elapsed else 0,
        'mean_ms': round(statistics.mean(latencies), 2),
        'median_ms': round(statistics.median(latencies), 2),
        'p95_ms': round(percentile(latencies, 95), 2),
        'p99_ms': round(percentile(latencies, 99), 2),
        'result': 'PASS' if success == args.requests else 'FAIL',
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if args.output:
        target = pathlib.Path(args.output)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print('Отчет:', target)
    if success != args.requests:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
