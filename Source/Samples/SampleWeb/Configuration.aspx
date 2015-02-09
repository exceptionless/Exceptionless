<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="Exceptionless" %>
<%@ Import Namespace="Exceptionless.Configuration" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<script runat="server">

    protected void Page_Load(object sender, EventArgs e) {}

    protected void forceButton_Click(object sender, EventArgs e) {
        ​SettingsManager.UpdateSettings(​ExceptionlessClient.Default.Configuration)​;
    }

</script>

<html xmlns="http://www.w3.org/1999/xhtml" >
    <head runat="server">
        <title></title>
    </head>
    <body>
        <form id="form1" runat="server">
            <div>
                <asp:Button ID="forceButton" runat="server" onclick="forceButton_Click" Text="Force Download" />
            </div>
        </form>
    </body>
</html>