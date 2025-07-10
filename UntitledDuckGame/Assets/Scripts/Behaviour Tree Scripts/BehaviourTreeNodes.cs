using System.Collections.Generic;

public abstract class Node {
    public string Name;

    protected Node(string name = "") {
        Name = name;
    }

    public abstract bool Execute(Dictionary<string, object> state);
}

public abstract class Composite : Node {
    protected List<Node> ChildNodes;

    protected Composite(List<Node> children, string name = "") : base(name) {
        ChildNodes = children;
    }
}

public class Sequence : Composite {
    public Sequence(List<Node> children, string name = "") : base(children, name) { }

    public override bool Execute(Dictionary<string, object> state) {
        foreach (var child in ChildNodes)
            if (!child.Execute(state))
                return false;
        return true;
    }
}

public class Selector : Composite {
    public Selector(List<Node> children, string name = "") : base(children, name) { }

    public override bool Execute(Dictionary<string, object> state) {
        foreach (var child in ChildNodes)
            if (child.Execute(state))
                return true;
        return false;
    }
}

public class Inverter : Node {
    private Node _child;

    public Inverter(Node child, string name = "") : base(name) {
        _child = child;
    }

    public override bool Execute(Dictionary<string, object> state) {
        return !_child.Execute(state);
    }
}


public class ActionNode : Node {
    private System.Func<Dictionary<string, object>, bool> _action;

    public ActionNode(System.Func<Dictionary<string, object>, bool> action, string name = "") : base(name) {
        _action = action;
    }

    public override bool Execute(Dictionary<string, object> state) {
        return _action(state);
    }
}

public class CheckNode : Node {
    private System.Func<Dictionary<string, object>, bool> _check;

    public CheckNode(System.Func<Dictionary<string, object>, bool> check, string name = "") : base(name) {
        _check = check;
    }

    public override bool Execute(Dictionary<string, object> state) {
        return _check(state);
    }
}
