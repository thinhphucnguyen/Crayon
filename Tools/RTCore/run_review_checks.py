#!/usr/bin/env python3
"""Compare the reviewed exporter with an untouched baseline using Unity's C# compiler."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline', type=Path, required=True)
parser.add_argument('--unity', type=Path, default=Path('/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app'))
args = parser.parse_args()
project = Path(__file__).resolve().parents[2]
mono_root = args.unity / 'Contents/Resources/Scripting/MonoBleedingEdge'
mono = mono_root / 'bin/mono'
csc = mono_root / 'lib/mono/4.5/csc.exe'
files = ['FixedPoint.cs', 'RTScene.cs', 'RTCameraMath.cs', 'BVHBuilder.cs', 'SceneSerializer.cs', 'RTSelfTest.cs']
with tempfile.TemporaryDirectory(prefix='ec551-checks-') as directory:
    temp = Path(directory)
    for label, source in [('baseline', args.baseline.expanduser()), ('reviewed', project)]:
        exe = temp / (label + '.exe')
        command = [str(mono), str(csc), '-nologo', '-langversion:latest', '-out:' + str(exe), str(project / 'Tools/RTCore/ParityChecks.cs')]
        command += [str(source / 'Assets/RTCore/Scripts/Runtime' / name) for name in files]
        subprocess.run(command, check=True)
        command = [str(mono), str(exe), str(temp / label)]
        if label == 'reviewed':
            command.append('regressions')
        subprocess.run(command, check=True)
    for original in (temp / 'baseline').glob('*.rtscene'):
        if original.read_bytes() != (temp / 'reviewed' / original.name).read_bytes():
            raise RuntimeError('Output differs: ' + original.name)
    print('PASS: all 151 output files are byte-identical.')
