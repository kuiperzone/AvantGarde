#!/bin/bash
find . -iname "bin" | xargs rm -rf
find . -iname "obj" | xargs rm -rf

rm -rf ./Publish/*