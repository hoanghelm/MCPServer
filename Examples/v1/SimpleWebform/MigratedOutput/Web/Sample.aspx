<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Sample.aspx.cs" Inherits="YourApp.Web.Sample" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Sample Migrated Web Form</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .container { max-width: 800px; margin: 0 auto; }
        .form-group { margin-bottom: 15px; }
        .btn { padding: 8px 16px; margin-right: 10px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h2>Sample Migrated Web Form</h2>
            <p>This demonstrates the migrated architecture with dependency injection.</p>
            
            <div class="form-group">
                <asp:Label runat="server" Text="Sample Data:"></asp:Label>
                <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="true" CssClass="table"></asp:GridView>
            </div>
            
            <div class="form-group">
                <asp:Button ID="LoadDataButton" runat="server" Text="Load Data" OnClick="LoadDataButton_Click" CssClass="btn" />
                <asp:Label ID="StatusLabel" runat="server" Text=""></asp:Label>
            </div>
        </div>
    </form>
</body>
</html>
