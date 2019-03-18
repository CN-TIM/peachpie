<?php

interface SessionBagInterface {
    public function getName();
}

final class SessionBagProxy implements SessionBagInterface {
    public function getName() {
        return __METHOD__;
    }
}

class A extends B {
    public function getPropertyValue($object) { return __METHOD__; }
}

abstract class B extends C implements I {
	
}

class C {

}

interface I {
    public function getPropertyValue($containingValue);
}

echo (new SessionBagProxy)->getName();
echo (new A)->getPropertyValue(NULL);

echo "Done.";