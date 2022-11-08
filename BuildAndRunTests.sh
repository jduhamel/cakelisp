#!/bin/sh

# Make sure Cakelisp is up to date
. ./Build.sh || exit $?

./bin/cakelisp --list-built-ins-details || exit $?

./bin/cakelisp --verbose-build-reasons --verbose-required-features-reasons \
			   runtime/Config_Linux.cake test/RunTests.cake || exit $?
