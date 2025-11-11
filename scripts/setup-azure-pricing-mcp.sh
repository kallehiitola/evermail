#!/bin/bash
set -e

# Azure Pricing MCP Setup Script for Evermail
# This script clones and sets up the Azure Pricing MCP server

echo "🚀 Setting up Azure Pricing MCP..."
echo ""

# Define installation directory
INSTALL_DIR="$HOME/azure-pricing-mcp"

# Check if already installed
if [ -d "$INSTALL_DIR" ]; then
    echo "⚠️  Azure Pricing MCP already exists at $INSTALL_DIR"
    read -p "Do you want to reinstall? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "❌ Setup cancelled"
        exit 1
    fi
    rm -rf "$INSTALL_DIR"
fi

# Clone repository
echo "📥 Cloning Azure Pricing MCP repository..."
git clone https://github.com/charris-msft/azure-pricing-mcp.git "$INSTALL_DIR"

# Navigate to directory
cd "$INSTALL_DIR"

# Run setup
echo ""
echo "🔧 Running setup (creating virtual environment and installing dependencies)..."
python setup.py

echo ""
echo "✅ Azure Pricing MCP setup complete!"
echo ""
echo "📝 Next steps:"
echo "1. Add to ~/.cursor/mcp.json:"
echo ""
echo '   "azure-pricing": {'
echo '     "command": "python",'
echo '     "args": ["-m", "azure_pricing_server"],'
echo "     \"cwd\": \"$INSTALL_DIR\""
echo '   }'
echo ""
echo "2. Restart Cursor (Cmd+Q on macOS, Alt+F4 on Windows/Linux)"
echo ""
echo "3. Test it by asking Cursor:"
echo '   "What is the price of Azure SQL Serverless in West Europe?"'
echo ""

