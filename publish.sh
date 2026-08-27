#!/bin/bash
pupnet -k appimage -r linux-x64 -y
pupnet -k rpm -r linux-x64 -y
pupnet -k deb -r linux-x64 -y

pupnet -k appimage -r linux-arm64 -y
pupnet -k rpm -r linux-arm64 -y
pupnet -k deb -r linux-arm64 -y