<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="UserManagement.aspx.cs" Inherits="UserManagement.UserManagement" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>User Management</title>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            <h2>User Management System</h2>
            <p>Welcome, <asp:Label ID="lblWelcome" runat="server"></asp:Label>!</p>
            <asp:Button ID="btnLogout" runat="server" Text="Logout" OnClick="btnLogout_Click" />
            <hr />
            
            <h3>Add/Edit User</h3>
            <table>
                <tr>
                    <td>Username:</td>
                    <td><asp:TextBox ID="txtUsername" runat="server"></asp:TextBox></td>
                </tr>
                <tr>
                    <td>Email:</td>
                    <td><asp:TextBox ID="txtEmail" runat="server"></asp:TextBox></td>
                </tr>
                <tr>
                    <td>Password:</td>
                    <td><asp:TextBox ID="txtPassword" runat="server" TextMode="Password"></asp:TextBox></td>
                </tr>
                <tr>
                    <td>Is Active:</td>
                    <td><asp:CheckBox ID="chkIsActive" runat="server" Checked="true" /></td>
                </tr>
                <tr>
                    <td colspan="2">
                        <asp:Button ID="btnSave" runat="server" Text="Save" OnClick="btnSave_Click" />
                        <asp:Button ID="btnUpdate" runat="server" Text="Update" OnClick="btnUpdate_Click" Visible="false" />
                        <asp:Button ID="btnCancel" runat="server" Text="Cancel" OnClick="btnCancel_Click" />
                        <asp:HiddenField ID="hdnUserID" runat="server" />
                    </td>
                </tr>
            </table>
            <asp:Label ID="lblMessage" runat="server" ForeColor="Red"></asp:Label>
            <br /><br />
            
            <h3>Users List</h3>
            <asp:GridView ID="gvUsers" runat="server" AutoGenerateColumns="false" 
                          OnRowCommand="gvUsers_RowCommand" DataKeyNames="userid">
                <Columns>
                    <asp:BoundField DataField="userid" HeaderText="ID" />
                    <asp:BoundField DataField="username" HeaderText="Username" />
                    <asp:BoundField DataField="email" HeaderText="Email" />
                    <asp:BoundField DataField="isactive" HeaderText="Active" />
                    <asp:BoundField DataField="createdate" HeaderText="Created" DataFormatString="{0:yyyy-MM-dd}" />
                    <asp:TemplateField HeaderText="Actions">
                        <ItemTemplate>
                            <asp:Button ID="btnEdit" runat="server" Text="Edit" 
                                        CommandName="EditUser" CommandArgument='<%# Eval("userid") %>' />
                            <asp:Button ID="btnDelete" runat="server" Text="Delete" 
                                        CommandName="DeleteUser" CommandArgument='<%# Eval("userid") %>' 
                                        OnClientClick="return confirm('Are you sure you want to delete this user?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </form>
</body>
</html>