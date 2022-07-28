import itertools
import subprocess
import sys
import re
import json

import requests

import version

# get release version
final_version = f"v{version.get_version(build=None)}"
print(f"release version: {final_version}")

# check if on master branch
if not "* master" in subprocess.check_output(["git", "branch"], stdin=None, stderr=None, shell=False).decode("utf8"):
    raise Exception("Must be on master branch.")

print("  master branch: OK")

# check if release version already exist
with open("solution.json", "r") as fh:
    solution_data = json.load(fh)

pattern = r"https:\/\/github.com\/(.*)"
request_url = re.sub(pattern, r"https://api.github.com/repos/\1/releases", solution_data["repository-url"])

headers = {
    "Authorization": f"token {sys.argv[1]}",
    "User-Agent": "Nexus",
    "Accept": "application/vnd.github.v3+json"
}

response = requests.get(request_url, headers=headers)
response.raise_for_status()
releases = response.json()

if final_version in (release["name"] for release in releases):
    raise Exception(f"Release {final_version} already exists.")

print(" unique release: OK")

# prompt for annotation
print("Please enter the release message (type 'quit' to stop):")
lines = itertools.takewhile(lambda x: x.strip() != "quit" and x.strip() != "quit()", sys.stdin)
release_message = "".join(lines).rstrip('\n')

# create tag
subprocess.check_output(["git", "tag", "-a", final_version, "-m", release_message, "--cleanup=whitespace"], stdin=None, stderr=None, shell=False)

print("     create tag: OK")

# push tag
subprocess.check_output(["git", "push", "--quiet", "origin", final_version], stdin=None, stderr=None, shell=False)
print("       push tag: OK")
