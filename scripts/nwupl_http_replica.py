#!/usr/bin/env python3
"""
NWUPL EAMS HTTP replica - based on tam.nwupl.edu.cn.har.

This script is a pure-HTTP (no Playwright) implementation skeleton derived from
the captured HAR. It can:

  * --analyze-har   : print the important HTTP flow extracted from the HAR
  * --live          : attempt the real login + grade/course-table requests
                      (requires network access to authserver.nwupl.edu.cn and
                       tam.nwupl.edu.cn)

The password encryption part is the main unknown: the HAR contains an already
encrypted password, but not the login page JavaScript that produced it. If you
run with --live, the script will try to read the login page and locate common
RSA fields (pwdEncryptSalt / publicKey / modulus). If those are not found it
will stop before submitting credentials.

Usage examples:
  python3 scripts/nwupl_http_replica.py --analyze-har /var/nfs_share/tam.nwupl.edu.cn.har
  python3 scripts/nwupl_http_replica.py --live --username 学号 --password '...'
"""

import argparse
import json
import re
import sys
from html.parser import HTMLParser
from urllib.parse import urlencode, urlparse

try:
    import requests
except ImportError:
    requests = None

AUTHSERVER_LOGIN = "https://authserver.nwupl.edu.cn/authserver/login"
EAMS_HOME = "https://tam.nwupl.edu.cn/eams/homeExt.action"
EAMS_GRADE_PAGE = "https://tam.nwupl.edu.cn/eams/teach/grade/course/person.action"
EAMS_GRADE_SEARCH = "https://tam.nwupl.edu.cn/eams/teach/grade/course/person!search.action"
EAMS_COURSE_TABLE_PAGE = "https://tam.nwupl.edu.cn/eams/courseTableForStd.action"
EAMS_COURSE_TABLE_DATA = "https://tam.nwupl.edu.cn/eams/courseTableForStd!courseTable.action"




def random_string(length):
    chars = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678"
    import random
    return "".join(random.choice(chars) for _ in range(length))


def encrypt_password(password, aes_key):
    """Replicates encryptPassword() in authserver's encrypt.js.

    AES-128-CBC/PKCS7, plaintext = randomString(64) + password,
    IV = randomString(16), key = aes_key (the pwdKey from login page).
    Returns the same format as CryptoJS.AES.encrypt(...).toString().
    """
    if not aes_key:
        return password

    try:
        from Cryptodome.Cipher import AES
        from Cryptodome.Util.Padding import pad
    except Exception:
        try:
            from Crypto.Cipher import AES
            from Crypto.Util.Padding import pad
        except Exception as exc:
            raise RuntimeError("PyCryptodome is required for password encryption") from exc

    data = (random_string(64) + password).encode("utf-8")
    key = aes_key.encode("utf-8")
    iv = random_string(16).encode("utf-8")

    cipher = AES.new(key, AES.MODE_CBC, iv)
    encrypted = cipher.encrypt(pad(data, AES.block_size))
    # With an explicit key/iv WordArray, CryptoJS does not add the
    # "Salted__" header; toString() is just base64(ciphertext).
    return __import__("base64").b64encode(encrypted).decode()


def extract_aes_key(html):
    patterns = [
        r'pwdDefaultEncryptSalt\s*=\s*["\']([^"\']+)["\']',
        r'pwdEncryptSalt\s*=\s*["\']([^"\']+)["\']',
        r'name="pwdEncryptSalt"[^>]*value="([^"]+)"',
        r'id="pwdEncryptSalt"[^>]*value="([^"]+)"',
        r'pwdKey\s*=\s*["\']([^"\']+)["\']',
    ]
    import re
    for pat in patterns:
        m = re.search(pat, html, re.I)
        if m:
            return m.group(1)
    return None




BROWSER_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
    "Accept-Language": "en,zh-CN;q=0.9,zh;q=0.8,en-GB;q=0.7,en-US;q=0.6",
    "Cache-Control": "no-cache",
    "Pragma": "no-cache",
    "Sec-Fetch-Dest": "document",
    "Sec-Fetch-Mode": "navigate",
    "Sec-Fetch-Site": "same-origin",
    "Sec-Fetch-User": "?1",
    "Upgrade-Insecure-Requests": "1",
    "sec-ch-ua": '"Not=A?Brand";v="99", "Microsoft Edge";v="151", "Chromium";v="151"',
    "sec-ch-ua-mobile": "?0",
    "sec-ch-ua-platform": '"Windows"',
}


def apply_browser_headers(session):
    for k, v in BROWSER_HEADERS.items():
        session.headers[k] = v


def need_requests():
    if requests is None:
        sys.exit("Python 'requests' module is required. Install with: pip install requests")


class HiddenInputExtractor(HTMLParser):
    """Extract <input type=hidden name=... value=...> from a login page."""
    def __init__(self):
        super().__init__()
        self.hidden = {}
        self._current = None

    def handle_starttag(self, tag, attrs):
        if tag.lower() != "input":
            return
        attrs = dict(attrs)
        if attrs.get("type", "").lower() in ("hidden", "text"):
            name = attrs.get("name")
            if name:
                self.hidden[name] = attrs.get("value", "")


def extract_login_fields(html):
    parser = HiddenInputExtractor()
    parser.feed(html)
    return parser.hidden


def analyze_har(har_path):
    with open(har_path, "r", encoding="utf-8") as f:
        har = json.load(f)

    entries = har["log"]["entries"]
    print(f"HAR entries: {len(entries)}\n")

    interesting = []
    for i, e in enumerate(entries):
        req = e["request"]
        url = req["url"]
        if any(k in url for k in [
            "authserver/login",
            "homeExt.action",
            "courseTableForStd",
            "teach/grade/course/person",
            "stdPreSelectedCourse",
        ]):
            interesting.append((i, req, e["response"]))

    for i, req, resp in interesting:
        print(f"[{i:3}] {req['method']:6} {resp['status']} {req['url']}")
        if "postData" in req:
            print(f"      POST body: {req['postData'].get('text', '')[:500]}")
        if resp.get("status") in (301, 302):
            loc = next((h["value"] for h in resp.get("headers", []) if h["name"].lower() == "location"), "")
            print(f"      Location: {loc}")


def live_login(session, username, password, args=None, service_url=EAMS_HOME):
    """Perform CAS login using only HTTP requests."""
    need_requests()
    apply_browser_headers(session)

    print(f"[1] GET login page: {AUTHSERVER_LOGIN}")
    login_url = f"{AUTHSERVER_LOGIN}?service={requests.utils.quote(service_url, safe='')}"
    r = session.get(login_url, timeout=30)
    r.raise_for_status()
    html = r.text

    fields = extract_login_fields(html)
    print(f"    Hidden fields found: {list(fields.keys())}")

    execution = fields.get("execution")
    if not execution:
        # Some CAS pages use 'execution' inside a script; try common regex.
        m = re.search(r'name="execution"\s+value="([^"]+)"', html)
        if m:
            execution = m.group(1)
    if not execution:
        sys.exit("Could not find 'execution' field. The login page may have changed or requires JS rendering.")

    lt = fields.get("lt", "")
    captcha = fields.get("captcha", "")

    # Password encryption is the main unknown. The HAR contains an encrypted
    # password produced by the login page's JS. We need either:
    #   - the login page HTML/JS to implement the exact RSA encryption, or
    #   - a pre-encrypted password from a fresh session.
    # Search for common RSA fields and fail loudly if we cannot find them.
    encrypt_hint = re.search(r'(pwdEncryptSalt|pwdDefaultEncryptSalt|modulus|publicKey|RSA\.?setPublic)', html, re.I)
    if not encrypt_hint:
        sys.exit(
            "Could not locate password encryption settings on the login page. "
            "Please provide the login page HTML or the JS used to encrypt the password."
        )

    print(f"    Found encryption hint: {encrypt_hint.group(0)}")

    # password encryption key from login page, or supplied via --aes-key
    aes_key = args.aes_key if args and hasattr(args, "aes_key") else None
    if not aes_key:
        aes_key = extract_aes_key(html)
    if not aes_key:
        sys.exit(
            "Could not find pwdEncryptSalt/pwdDefaultEncryptSalt on login page. "
            "Pass it manually with --aes-key."
        )

    if getattr(args, "encrypted_password", None):
        encrypted_password = args.encrypted_password
        print("[i] Using pre-encrypted password from --encrypted-password")
    else:
        encrypted_password = encrypt_password(password, aes_key)
        print("[i] Password encrypted with AES key found on login page")

    data = {
        "username": username,
        "password": encrypted_password,
        "captcha": captcha,
        "rememberMe": "true",
        "_eventId": "submit",
        "cllt": "userNameLogin",
        "dllt": "generalLogin",
        "lt": lt,
        "execution": execution,
    }

    print(f"[2] POST login: {login_url}")
    post_headers = {
        "Origin": "https://authserver.nwupl.edu.cn",
        "Referer": login_url,
        "Content-Type": "application/x-www-form-urlencoded",
    }
    r = session.post(login_url, data=data, headers=post_headers, allow_redirects=False, timeout=30)
    print(f"    Status: {r.status_code}")
    loc = r.headers.get("Location", "")
    print(f"    Location: {loc}")

    if r.status_code in (301, 302) and "ticket=" in loc:
        print("[3] Follow ticket redirect")
        r = session.get(loc, timeout=30)
        r.raise_for_status()
        print(f"    Final URL: {r.url}")
        return session
    else:
        sys.exit(f"Login failed, status={r.status_code}, location={loc}")


def fetch_grade(session, semester_id="194", project_type=""):
    print(f"\n[grade] GET {EAMS_GRADE_PAGE}")
    r = session.get(EAMS_GRADE_PAGE, timeout=30)
    r.raise_for_status()

    print(f"[grade] GET {EAMS_GRADE_SEARCH}?semesterId={semester_id}&projectType={project_type}")
    r = session.get(EAMS_GRADE_SEARCH, params={"semesterId": semester_id, "projectType": project_type}, timeout=30)
    r.raise_for_status()
    return r.text


def fetch_course_table(session, semester_id="194", project_id="1", ids="676535", kind="std"):
    print(f"\n[course] GET {EAMS_COURSE_TABLE_PAGE}")
    r = session.get(EAMS_COURSE_TABLE_PAGE, timeout=30)
    r.raise_for_status()

    print(f"[course] POST {EAMS_COURSE_TABLE_DATA}")
    data = {
        "ignoreHead": "1",
        "setting.kind": kind,
        "startWeek": "",
        "project.id": project_id,
        "semester.id": semester_id,
        "ids": ids,
    }
    r = session.post(EAMS_COURSE_TABLE_DATA, data=data, headers={"X-Requested-With": "XMLHttpRequest"}, timeout=30)
    r.raise_for_status()
    return r.text




def parse_grade_rows(html):
    """Very small parser for /eams/teach/grade/course/person!search.action HTML."""
    rows = []
    # tbody rows: <tr> ... <td>...</td> ... </tr>
    body_match = re.search(r'<tbody[^>]*>(.*?)</tbody>', html, re.S)
    if not body_match:
        return rows
    for tr in re.findall(r'<tr[^>]*>(.*?)</tr>', body_match.group(1), re.S):
        cells = [re.sub(r'<[^>]+>', '', td).strip() for td in re.findall(r'<td[^>]*>(.*?)</td>', tr, re.S)]
        if len(cells) >= 4:
            rows.append(cells)
    return rows


def count_course_activities(html):
    """Count TaskActivity(...) occurrences from courseTableForStd!courseTable.action response."""
    return len(re.findall(r'new\s+TaskActivity\(', html))


def parse_har_data(har_path):
    with open(har_path, "r", encoding="utf-8") as f:
        har = json.load(f)
    entries = har["log"]["entries"]
    grade_idxs = [i for i, e in enumerate(entries) if "person!search.action" in e["request"]["url"]]
    course_idxs = [i for i, e in enumerate(entries) if "courseTableForStd!courseTable.action" in e["request"]["url"]]

    print("=== Grade rows from HAR ===")
    for i in grade_idxs[:1]:
        html = entries[i]["response"]["content"].get("text", "")
        rows = parse_grade_rows(html)
        print(f"entry {i}: {len(rows)} rows")
        for row in rows[:5]:
            print("  ", row)

    print("\n=== Course table activities from HAR ===")
    for i in course_idxs[:1]:
        html = entries[i]["response"]["content"].get("text", "")
        print(f"entry {i}: {count_course_activities(html)} TaskActivity occurrences")

def main():
    parser = argparse.ArgumentParser(description="NWUPL EAMS pure HTTP replica")
    parser.add_argument("--analyze-har", metavar="HAR")
    parser.add_argument("--parse-har", metavar="HAR", help="parse sample data from HAR without network")
    parser.add_argument("--live", action="store_true", help="attempt live HTTP login")
    parser.add_argument("--username")
    parser.add_argument("--password")
    parser.add_argument("--encrypted-password", help="pre-encrypted password from a fresh HAR")
    parser.add_argument("--aes-key", help="AES key used by the login page (pwdEncryptSalt/pwdDefaultEncryptSalt)")
    parser.add_argument("--proxy", help="HTTP/HTTPS proxy, e.g. http://192.168.0.69:6152")
    parser.add_argument("--semester-id", default="194")
    parser.add_argument("--project-id", default="1")
    parser.add_argument("--ids", default="676535")
    args = parser.parse_args()

    if args.analyze_har:
        analyze_har(args.analyze_har)
        return

    if args.parse_har:
        parse_har_data(args.parse_har)
        return

    if not args.live:
        parser.print_help()
        return

    need_requests()
    if not args.username or not args.password:
        sys.exit("--live requires --username and --password (or --encrypted-password)")

    session = requests.Session()
    if args.proxy:
        session.proxies = {
            "http": args.proxy,
            "https": args.proxy,
        }
    live_login(session, args.username, args.password, args)

    grade_html = fetch_grade(session, args.semester_id)
    print(f"\nGrade HTML length: {len(grade_html)}")
    print(grade_html[:1000])

    course_html = fetch_course_table(session, args.semester_id, args.project_id, args.ids)
    print(f"\nCourse table HTML length: {len(course_html)}")
    print(course_html[:1000])


if __name__ == "__main__":
    main()
