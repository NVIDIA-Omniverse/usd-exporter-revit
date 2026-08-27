# SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
import glob
import logging
import os
import shutil
import subprocess
import unittest
from pathlib import Path

# isort: off
from test_checkUsd import CheckUsd
from pxr import Gf
import omni.asset_validator as validator
import omni.repo.man

# isort: on

RAC_BASIC_DOWNLOAD_URLS = {
    "2024": "https://help.autodesk.com/view/RVT/2024/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88",
    "2025": "https://help.autodesk.com/view/RVT/2025/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88",
    "2026": "https://help.autodesk.com/view/RVT/2026/ENU/?guid=GUID-61EF2F22-3A1F-4317-B925-1E85F138BE88",
}

# AUTOREMOVE: BEGIN
TRANSFORM_TEST_CASES = {
    "transform_test": [
        {"primPath": "/tn__transform_test_3D_xd1u0/Instances/tn__MechanicalEquipment_zJ/tn__TETanks_f7/tn__TETanks_f7/tn__3237_", "translation": [618.15, 905.06, 0]},
        {"primPath": "/tn__transform_test_3D_xd1u0/Instances/tn__MechanicalEquipment_zJ/tn__TETanks_f7/tn__TETanks_f7/tn__3236_", "translation": [599.15, 905.06, 0]},
    ],
    "transform_test2": [
        {"primPath": "/tn__transform_test2_3D_xh1x0/Instances/tn__GenericModels_qD/PT_PBA_PAINT/tn__ACCENTDARK_mA/tn__5579_", "translation": [0, 0, 0]},
    ],
}
# AUTOREMOVE: END


def _resolve_build_path(dir: str) -> Path:
    return Path(omni.repo.man.resolve_tokens("${test_root}/" + dir))


def _load_inputs_root():
    """Resolve the configured Revit test input root (override via --set-token test_inputs_root:...)."""
    return omni.repo.man.resolve_tokens("${test_inputs_root}")


class TestRevitUsdExport(unittest.TestCase):
    def setUp(self):
        # Enabling color text in PowerShell.
        os.system("")
        # file comparison tolerance
        self.FILE_SIZE_TOLERANCE = 0.001
        self._inputs_root = _load_inputs_root()
        # location of golden usda files (by version)
        self._outputs_golden_folder = omni.repo.man.resolve_tokens("${root}/tests/outputs")
        # output location
        self._outputs_temp_folder = omni.repo.man.resolve_tokens("${root}/_testoutput")
        # converter
        self._revit_batch_cli = omni.repo.man.resolve_tokens("${root}/_build/test/windows-x86_64/${config}/RevitTestHarness.exe")
        # placeholder value for version. derived classes set self._ver before super().setUp()
        if not hasattr(self, "_ver"):
            self._ver = "0"
        # logger so cleanup is safe even if a test skips before test_generic runs.
        self._logger = logging.getLogger(f"test-revit-{self._ver}")
        # asset validator
        self.validation_engine = omni.asset_validator.ValidationEngine()
        self.validation_engine.disable_rule(omni.asset_validator.UsdAsciiPerformanceChecker)
        self.validation_engine.disable_rule(omni.asset_validator.MaterialPathChecker)
        self.validation_engine.disable_rule(omni.asset_validator.MissingReferenceChecker)
        self._processes = []

    def tearDown(self):
        """Ensure all processes are terminated and addins are removed after each test"""
        self._cleanup_all_processes()
        if getattr(self, "_setup_exe", None) is not None:
            self.__uninstall_addins(assert_success=False)

    def _cleanup_all_processes(self):
        """Terminate all processes started by this test"""
        for proc in self._processes:
            try:
                if proc.poll() is None:  # Process still running
                    proc.terminate()
                    try:
                        proc.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        # If terminate failed, force kill the process
                        self._logger.warning(f"Process {proc.pid} did not terminate, forcing kill")
                        proc.kill()
                        proc.wait()
            except Exception as e:
                self._logger.warning(f"Failed to cleanup process {proc.pid}: {e}")
        self._processes.clear()

    def __run_subprocess_in_dir(self, working_dir, command, *argv):
        """Run subprocess in a specified working directory, returning the return code and output logs"""
        cmdline = list()
        cmdline.append(command)
        cmdline += argv
        self._logger.info(str(cmdline))
        # Use Popen to track the process
        proc = subprocess.Popen(cmdline, cwd=working_dir, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        self._processes.append(proc)
        # Wait for completion
        stdout, _ = proc.communicate()
        return proc.returncode, stdout.decode("utf-8")

    def __run_subprocess(self, command, *argv):
        """Run subprocess in the current directory, returning the return code and output logs"""
        return self.__run_subprocess_in_dir(os.getcwd(), command, *argv)

    def __batch_convert_to_usd(self, input_folder, output_folder, version) -> str:
        """Sends folder to CLI to batch convert to usd"""
        self._logger.info(f"Running tests for version: {version}")
        self.assertTrue(os.path.exists(self._revit_batch_cli), msg="Revit batch cli not found! Check build success")
        return_code, output = self.__run_subprocess(self._revit_batch_cli, input_folder, "-v ", version, "-o ", output_folder)
        self._logger.info(output)
        self.assertEqual(return_code, 0)

    def __validate_usd(self, usd_file):
        """Run the Asset Validator over the given usd file and assert on any unexpected errors."""
        result = self.validation_engine.validate(usd_file)
        self.assertEqual(result.asset, usd_file)
        self.assertFalse(result.issues(), msg=result.issues())

    def __check_output(self, output_folder, golden_output_folder):
        """Validates output usdas and compares the output folder with USDAs to the other"""

        def files_almost_equal(file_path1, file_path2):
            """Check that the file sizes are nearly equal"""
            a = os.path.getsize(file_path1)
            b = os.path.getsize(file_path2)
            return abs(a - b) <= max(self.FILE_SIZE_TOLERANCE * max(a, b), 0.0)  # allow 0.1% file size difference

        output_files = glob.glob(output_folder + "/*.usda", recursive=True)
        for output_file in output_files:
            # validate output
            self.__validate_usd(output_file)
            # compare to golden usda
            relative_path = os.path.relpath(output_file, output_folder)
            golden_output_file = os.path.join(golden_output_folder, relative_path)
            self.assertTrue(os.path.exists(golden_output_file), f"No golden output for comparison at path {golden_output_file} for output file {output_file}")
            self._logger.info(f"Ensuring {output_file} and {golden_output_file} are not the same")
            self.assertTrue(output_file != golden_output_file, msg=f"")
            self._logger.info(f"Comparing filesize {output_file} to {golden_output_file}")
            self.assertTrue(files_almost_equal(output_file, golden_output_file))
        # Ensure that all golden usds are covered.
        golden_output_files = glob.glob(golden_output_folder + "/*.usda", recursive=True)
        for golden_output_file in golden_output_files:
            relative_path = os.path.relpath(golden_output_file, golden_output_folder)
            output_file = os.path.join(output_folder, relative_path)
            self.assertTrue(os.path.exists(output_file), f"No matching output file to golden file {golden_output_file}")


    # Check if the old lights are reflected from rac_basic_sample_project.
    def __check_output_basic_sample_usd(self, file_path):
        self._logger.info(f"Check rac_basic_sample_project : {file_path}")
        checkUsd = CheckUsd(file_path, self)
        defaultPrimName = checkUsd.get_defaultPrim()
        baseLightPath = f"{defaultPrimName}/Lights"
        childPrimNames = checkUsd.get_childPrimNames(baseLightPath)
        sphereLights = 0
        cylinderLights = 0
        if childPrimNames != None:
            for name in childPrimNames:
                typeName = checkUsd.get_primTypeName(f"{baseLightPath}/{name}").lower()
                if typeName == "spherelight":
                    sphereLights += 1
                if typeName == "cylinderlight":
                    cylinderLights += 1
        self._logger.info(f"SphereLight = {sphereLights}")
        self._logger.info(f"CylinderLight = {cylinderLights}")
        self.assertTrue(sphereLights == 8 and cylinderLights == 2, f'"{file_path}" The number of exported lights varies.')

    def __check_output_basic_sample_usds(self, output_folder, file_name):
        """Validates rac_basic_sample_project"""
        output_files = glob.glob(f"{output_folder}/{file_name}/fullProp/**/{file_name}*.usda", recursive=True)
        self.assertTrue(len(output_files) > 0, f"No USD files found matching pattern: {output_folder}/{file_name}/fullProp/**/{file_name}*.usda")
        for output_file in output_files:
            self.__check_output_basic_sample_usd(output_file)


    def __missing_rvt_message(self, case_dir):
        download_url = RAC_BASIC_DOWNLOAD_URLS.get(self._ver, "tests/LICENSE.md")
        case_dir = case_dir.replace(os.sep, "/")
        return f"Missing Revit model (.rvt) in {case_dir}.\n" f"Download rac_basic_sample_project.rvt for Revit {self._ver} from Autodesk sample projects:\n" f"  {download_url}\n" f"See tests/LICENSE.md and tests/inputs/README.md for details."

    def __preflight_case_dirs(self, input_folder_path):
        """Fail fast when a case folder has export settings but no Revit model."""
        for case_name in os.listdir(input_folder_path):
            case_dir = os.path.join(input_folder_path, case_name)
            if not os.path.isdir(case_dir):
                continue
            json_files = glob.glob(os.path.join(case_dir, "*.json"))
            rvt_files = glob.glob(os.path.join(case_dir, "*.rvt"))
            if json_files and not rvt_files:
                self.fail(self.__missing_rvt_message(case_dir))

    def __validate_case_outputs(self, input_folder_path, output_folder_path):
        """Run registered validators for each converted case folder."""
        for case_name in os.listdir(input_folder_path):
            case_dir = os.path.join(input_folder_path, case_name)
            if not os.path.isdir(case_dir):
                continue
            if case_name == "rac_basic":
                self.__check_output_basic_sample_usds(output_folder_path, case_name)

    def __cleanup_output(self, directory_path):
        """Remove the given directory tree if it exists."""
        if not os.path.exists(directory_path):
            return
        shutil.rmtree(directory_path)

    def __convert_folder(self, input_folder_path, output_folder_path, golden_output_folder_path, version):
        self.assertTrue(os.path.exists(input_folder_path))
        self.__preflight_case_dirs(input_folder_path)
        # Ensure we have a clean start
        self.__cleanup_output(output_folder_path)
        try:
            # Convert
            self.__batch_convert_to_usd(input_folder_path, output_folder_path, version)
            # Validation & rudimentary comparison
            # self.__check_output(output_folder_path, golden_output_folder_path)
            self.__validate_case_outputs(input_folder_path, output_folder_path)
        finally:
            # Cleanup even if conversion or validation fails
            self.__cleanup_output(output_folder_path)

    def __test_version(self, version):
        input_folder_path = os.path.join(self._inputs_root, version)
        if not os.path.isdir(input_folder_path):
            self.skipTest(f"No test inputs found for Revit {version} under {input_folder_path}")

        inputs_root_name = os.path.basename(self._inputs_root)
        output_folder_path = os.path.join(self._outputs_temp_folder, inputs_root_name, version)
        golden_output_folder_path = os.path.join(self._outputs_golden_folder, version, inputs_root_name)
        self.__convert_folder(input_folder_path, output_folder_path, golden_output_folder_path, version)

    def __install_addins(self):
        lib_dir = _resolve_build_path("")
        self._logger.info("Installing Revit Addins")
        return_code, output = self.__run_subprocess_in_dir(lib_dir, self._setup_exe, "/install")
        self._logger.info(output)
        self.assertEqual(return_code, 0)

    def __uninstall_addins(self, assert_success=True):
        lib_dir = _resolve_build_path("")
        self._logger.info("Uninstalling Revit Addins")
        # setup must be run in specific directory
        return_code, output = self.__run_subprocess_in_dir(lib_dir, self._setup_exe, "/uninstall")
        self._logger.info(output)
        if assert_success:
            self.assertEqual(return_code, 0)
        elif return_code != 0:
            # In the teardown path we don't fail the test on cleanup errors
            self._logger.warning(f"Uninstalling Revit Addins returned nonzero exit code {return_code}")

    # ==================================================================================================================
    # Testing
    # ==================================================================================================================
    def test_generic(self):
        """Common test case for each derived class testing a specific Revit version."""
        if self.__class__ is TestRevitUsdExport:
            self.skipTest("Only derived classes of TestRevitUsdExport should run this test.")
        self._setup_exe = _resolve_build_path(f"RevitUsdExportSetup{self._ver}.exe")
        # Ensure no previous version is installed (e.g. from failed test run)
        self.__uninstall_addins(assert_success=True)
        self.__install_addins()
        self.__test_version(self._ver)
