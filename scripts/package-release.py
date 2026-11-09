"""Build a versioned download from release-package.json; Python standard library only."""
from pathlib import Path
import hashlib
import json
import shutil
import tempfile
import zipfile
import io
import re

ROOT = Path(__file__).resolve().parents[1]
CONFIG = json.loads((ROOT / 'release-package.json').read_text(encoding='utf-8'))
OUTPUT = ROOT.parent / f"{CONFIG['slug']}-{CONFIG['version']}"
if OUTPUT.parent.resolve() != ROOT.parent.resolve() or OUTPUT.resolve() == ROOT.resolve():
    raise SystemExit('Unsafe release destination')
EXCLUDED = {'.git', 'node_modules', '__pycache__', '.npm-cache', '.validation', 'tests', 'test', 'target', 'build', 'dist', 'superpowers'}
PRIVATE = {'credentials.dat', 'last-stream-diagnostics.json', 'accounts.vault', 'vault.bin', 'vault.key'}

def allowed(path):
    return not any(part in EXCLUDED for part in path.parts) and path.name not in PRIVATE and path.suffix not in {'.log', '.pyc', '.bak', '.tmp'}

def copy_entry(source, destination):
    if not source.exists():
        raise FileNotFoundError(f'Build input missing: {source}. Run the build documented in docs/BUILDING.md first.')
    if source.is_file():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    else:
        destination.mkdir(parents=True, exist_ok=True)
        for file in source.rglob('*'):
            relative = file.relative_to(source)
            if file.is_file() and allowed(relative):
                copy_entry(file, destination / relative)

def validate_minecraft(folder):
    fabric = folder / 'minecraft-account-manager-fabric.jar'
    with zipfile.ZipFile(fabric) as archive:
        assert archive.testzip() is None
        metadata = json.loads(archive.read('fabric.mod.json'))
        assert metadata['name'] == 'Minecraft Account Manager'
        assert metadata['id'] == 'accountswitcher'
        children = metadata['jars']
        assert len(children) == 18, 'Expected shared core plus 17 exact-version platform JARs'
        for child in children:
            with zipfile.ZipFile(io.BytesIO(archive.read(child['file']))) as nested:
                assert nested.testzip() is None
    with zipfile.ZipFile(folder / 'minecraft-account-manager-forge-1.8.9.jar') as archive:
        assert archive.testzip() is None
        assert any(name.endswith('/AccountSwitcher.class') for name in archive.namelist())

with tempfile.TemporaryDirectory(prefix='project-release-', dir=ROOT.parent) as temp:
    staging = Path(temp) / OUTPUT.name
    staging.mkdir()
    for entry in CONFIG['files']:
        source = (ROOT / entry['from']).resolve()
        if not source.is_relative_to(ROOT.parent.resolve()):
            raise SystemExit('Input outside project')
        destination = staging / entry['to']
        if not destination.resolve().is_relative_to(staging.resolve()):
            raise SystemExit('Unsafe relative output')
        copy_entry(source, destination)
    if CONFIG['slug'] == 'MinecraftAccountManager':
        validate_minecraft(staging)
    # Include all common docs and project-specific guides. Runtime source remains
    # excluded except where the interpreter/plugin loader requires it.
    copy_entry(ROOT / 'docs', staging / 'docs')
    copy_entry(ROOT / 'CHANGELOG.md', staging / 'CHANGELOG.md')
    for license_file in ROOT.glob('LICENSE*'):
        copy_entry(license_file, staging / license_file.name)
    installation = (ROOT / 'docs/INSTALLATION.md').read_text(encoding='utf-8')
    # Remove the source-doc navigation if present; keep user-facing instructions.
    installation = '\n'.join(line for line in installation.splitlines()[1:] if not line.startswith('[Overview]'))
    (staging / 'README.md').write_text(
        f"# {CONFIG['title']} {CONFIG['version']}\n\n" + installation.strip() +
        '\n\n[Documentation](docs/README.md) · [Changelog](CHANGELOG.md)\n', encoding='utf-8')
    # Installation text was written for docs/; the release overview lives one level up.
    readme = staging / 'README.md'
    readme.write_text(re.sub(r'\]\((?!docs/|CHANGELOG\.md)([A-Z][A-Z-]*\.md)\)', r'](docs/\1)', readme.read_text(encoding='utf-8')), encoding='utf-8')
    for doc in (staging / 'docs').glob('*.md'):
        def resolve_source_link(match):
            label, target = match.groups()
            if '://' in target or target.startswith('#'):
                return match.group(0)
            relative = target.split('#')[0]
            if relative and not (doc.parent / relative).exists() and (ROOT / 'docs' / relative).exists():
                # Developer code paths refer to the separate source download.
                return f'{label} (`{relative.removeprefix("../")}` in the source download)'
            return match.group(0)
        doc.write_text(re.sub(r'\[([^\]]+)\]\(([^)]+)\)', resolve_source_link, doc.read_text(encoding='utf-8')), encoding='utf-8')
    hashes = []
    for file in sorted(staging.rglob('*')):
        if file.is_file():
            hashes.append(f"{hashlib.sha256(file.read_bytes()).hexdigest()}  {file.relative_to(staging).as_posix()}")
    (staging / 'SHA256SUMS').write_text('\n'.join(hashes) + '\n', encoding='utf-8')
    # Only the explicitly computed sibling release is replaced, after all inputs
    # have been staged and validated. Source and other versions are untouched.
    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    staging.rename(OUTPUT)
print(f'Release: {OUTPUT}')
